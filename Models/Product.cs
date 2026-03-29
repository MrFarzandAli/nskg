using System.ComponentModel.DataAnnotations;

namespace Nskg.Models;

public class Product
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0.01, 1_000_000)]
    [DataType(DataType.Currency)]
    public decimal Price { get; set; }

    [Display(Name = "Created On")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
