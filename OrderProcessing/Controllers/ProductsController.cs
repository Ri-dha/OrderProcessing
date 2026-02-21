using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application.Features;
using OrderProcessing.Domain.errors;
using OrderProcessing.Features;
using OrderProcessing.Infrastructure.Persistence;
using Wolverine;

namespace OrderProcessing.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductRequest request,
        [FromServices] IMessageBus bus,
        CancellationToken ct)
    {
        try
        {
            var response = await bus.InvokeAsync<CreatedResponse>(
                new CreateProductCommand(request.Name, request.Sku, request.Price, request.InitialStock), ct);
            return Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> CreateProductsBulk(
        [FromBody] CreateProductsBulkRequest request,
        [FromServices] IMessageBus bus,
        CancellationToken ct)
    {
        try
        {
            var command = new CreateProductsBulkCommand(request.Products
                .Select(x => new CreateProductCommand(x.Name, x.Sku, x.Price, x.InitialStock))
                .ToArray());

            var response = await bus.InvokeAsync<BulkCreatedResponse>(command, ct);
            return Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromServices] AppDbContext db, CancellationToken ct)
    {
        var products = await db.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ProductResponse(x.Id, x.Name, x.Sku, x.Price, x.AvailableStock, x.IsDeleted))
            .ToListAsync(ct);

        return Ok(products);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductRequest request,
        [FromServices] IMessageBus bus,
        CancellationToken ct)
    {
        try
        {
            var response = await bus.InvokeAsync<ProductResponse>(
                new UpdateProductCommand(id, request.Name, request.Sku, request.Price, request.Stock, request.IsDeleted), ct);
            return Ok(response);
        }
        catch (DomainValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
