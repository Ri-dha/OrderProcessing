using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application.Features;
using OrderProcessing.Application.Features.Contracts;
using OrderProcessing.Domain.errors;
using OrderProcessing.Infrastructure.Persistence;
using Wolverine;
using Wolverine.Http;

namespace OrderProcessing.Features;

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
    public static async Task<IResult> GetProducts(AppDbContext db, CancellationToken ct)
    {
        var products = await db.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ProductResponse(x.Id, x.Name, x.Sku, x.Price, x.AvailableStock, x.IsDeleted))
            .ToListAsync(ct);

        return Results.Ok(products);
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
