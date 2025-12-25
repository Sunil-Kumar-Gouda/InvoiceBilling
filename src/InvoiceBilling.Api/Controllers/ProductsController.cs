using InvoiceBilling.Api.Dtos.Products;
using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly InvoiceBillingDbContext _db;

    public ProductsController(InvoiceBillingDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> Get()
    {
        var items = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                UnitPrice = p.UnitPrice,
                CurrencyCode = p.CurrencyCode,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id)
    {
        var item = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Id == id)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                UnitPrice = p.UnitPrice,
                CurrencyCode = p.CurrencyCode,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt
            })
            .FirstOrDefaultAsync();

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Post([FromBody] CreateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
            return ValidationProblem(ModelState);
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim(),
            UnitPrice = request.UnitPrice,
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "INR" : request.CurrencyCode.Trim().ToUpperInvariant(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var dto = new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Sku = product.Sku,
            UnitPrice = product.UnitPrice,
            CurrencyCode = product.CurrencyCode,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt
        };

        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, [FromBody] UpdateProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
            return ValidationProblem(ModelState);
        }

        var product = await _db.Products
            .Where(p => p.IsActive && p.Id == id)
            .FirstOrDefaultAsync();

        if (product is null)
            return NotFound();

        product.Name = request.Name.Trim();
        product.Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim();
        product.UnitPrice = request.UnitPrice;
        product.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "INR" : request.CurrencyCode.Trim().ToUpperInvariant();

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await _db.Products
            .Where(p => p.IsActive && p.Id == id)
            .FirstOrDefaultAsync();

        if (product is null)
            return NotFound();

        product.IsActive = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
