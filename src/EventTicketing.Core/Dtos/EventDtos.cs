using System.ComponentModel.DataAnnotations;

namespace EventTicketing.Core.Dtos;

public class PricingTierRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(1, int.MaxValue)]
    public int Capacity { get; set; }
}

public class PricingTierResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public int TicketsSold { get; set; }
    public int Remaining { get; set; }
}

public class CreateEventRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Venue { get; set; } = string.Empty;

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public TimeOnly Time { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "TotalCapacity must be at least 1.")]
    public int TotalCapacity { get; set; }

    [Required, MinLength(1, ErrorMessage = "At least one pricing tier is required.")]
    public List<PricingTierRequest> PricingTiers { get; set; } = new();
}

public class UpdateEventRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Venue { get; set; } = string.Empty;

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public TimeOnly Time { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "TotalCapacity must be at least 1.")]
    public int TotalCapacity { get; set; }
}

public class EventResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public int TotalCapacity { get; set; }
    public List<PricingTierResponse> PricingTiers { get; set; } = new();
}
