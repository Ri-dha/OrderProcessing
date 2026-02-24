using OrderProcessing.Application.Features;
using OrderProcessing.Application.Features.Contracts;
using OrderProcessing.Domain.errors;
using Wolverine;
using Wolverine.Http;

namespace OrderProcessing.Features.Endpoints;

public static class ProductsEndpoints
{
    [WolverinePost("/api/products")]
    public static async Task<IResult> CreateProduct(CreateProductRequest request, IMessageBus bus, CancellationToken ct)
    {
        try
        {
            var response = await bus.InvokeAsync<CreatedResponse>(
                new CreateProductCommand(request.Name, request.Sku, request.Price, request.InitialStock), ct);
            return Results.Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    [WolverinePost("/api/products/bulk")]
    public static async Task<IResult> CreateProductsBulk(CreateProductsBulkRequest request, IMessageBus bus,
        CancellationToken ct)
    {
        try
        {
            var command = new CreateProductsBulkCommand(request.Products
                .Select(x => new CreateProductCommand(x.Name, x.Sku, x.Price, x.InitialStock))
                .ToArray());

            var response = await bus.InvokeAsync<BulkCreatedResponse>(command, ct);
            return Results.Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    [WolverineGet("/api/products")]
    public static async Task<IResult> GetProducts(IMessageBus bus, int? page = null, int? pageSize = null,
        CancellationToken ct = default)
    {
        try
        {
            var response = await bus.InvokeAsync<PagedResponse<ProductResponse>>(new GetProductsQuery(page, pageSize), ct);
            return Results.Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    [WolverinePut("/api/products/{id:guid}")]
    public static async Task<IResult> UpdateProduct(Guid id, UpdateProductRequest request, IMessageBus bus,
        CancellationToken ct)
    {
        try
        {
            var response = await bus.InvokeAsync<ProductResponse>(
                new UpdateProductCommand(id, request.Name, request.Sku, request.Price, request.Stock, request.IsDeleted), ct);
            return Results.Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
