using System.ComponentModel.DataAnnotations;

namespace OrderProcessing.Features;

public sealed class CreateProductRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(50, MinimumLength = 1)]
    public string Sku { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; init; }

    [Range(0, int.MaxValue)]
    public int InitialStock { get; init; }
}

public sealed class CreateProductsBulkRequest
{
    [Required, MinLength(1)]
    public IReadOnlyList<CreateProductRequest> Products { get; init; } = [];
}

public sealed class UpdateProductRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(50, MinimumLength = 1)]
    public string Sku { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; init; }

    [Range(0, int.MaxValue)]
    public int Stock { get; init; }

    public bool IsDeleted { get; init; }
}
