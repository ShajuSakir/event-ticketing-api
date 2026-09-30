using System.Text;
using EventTicketing.Api.Middleware;
using EventTicketing.Core.Constants;
using EventTicketing.Core.Entities;
using EventTicketing.Core.Interfaces;
using EventTicketing.Infrastructure.Data;
using EventTicketing.Infrastructure.Security;
using EventTicketing.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Event Ticketing API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Paste the JWT from POST /api/auth/login (no need to type \"Bearer \").",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=eventticketing.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<JwtTokenGenerator>();

var jwtSection = builder.Configuration.GetSection("Jwt");

var jwtSecret = jwtSection["Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured. Set it using User Secrets or an environment variable.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSection["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSection["Audience"],
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

builder.Services.AddAuthorization();

builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (app.Environment.IsDevelopment())
    {
        await SeedDemoUsersAsync(db);
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();


// Seeds two demo accounts on first run only, so the API is testable immediately without a registration endpoint.
static async Task SeedDemoUsersAsync(AppDbContext db)
{
    if (await db.Users.AnyAsync())
        return;

    var (adminHash, adminSalt) = PasswordHasher.Hash("Admin123!");
    var (customerHash, customerSalt) = PasswordHasher.Hash("Customer123!");

    db.Users.AddRange(
        new User { Id = Guid.NewGuid(), Username = "admin", Email = "admin@example.com", PasswordHash = adminHash, PasswordSalt = adminSalt, Role = Roles.Admin, CreatedAtUtc = DateTime.UtcNow },
        new User { Id = Guid.NewGuid(), Username = "customer", Email = "customer@example.com", PasswordHash = customerHash, PasswordSalt = customerSalt, Role = Roles.Customer, CreatedAtUtc = DateTime.UtcNow }
    );

    await db.SaveChangesAsync();
}

public partial class Program { }
