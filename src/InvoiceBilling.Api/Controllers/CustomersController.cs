using InvoiceBilling.Api.Dtos.Customers;
using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly InvoiceBillingDbContext _db;

    public CustomersController(InvoiceBillingDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> Get()
    {
        var customers = await _db.Customers
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CustomerDto
            {
                Id = c.Id,
                Name = c.Name,
                BusinessName = c.BusinessName,
                Email = c.Email,
                Phone = c.Phone,
                BillingAddress = c.BillingAddress,
                TaxId = c.TaxId,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return Ok(customers);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> GetById(Guid id)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .Where(c => c.IsActive && c.Id == id)
            .Select(c => new CustomerDto
            {
                Id = c.Id,
                Name = c.Name,
                BusinessName = c.BusinessName,
                Email = c.Email,
                Phone = c.Phone,
                BillingAddress = c.BillingAddress,
                TaxId = c.TaxId,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (customer is null)
            return NotFound();

        return Ok(customer);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Post([FromBody] CreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
            return ValidationProblem(ModelState);
        }

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            BusinessName = request.BusinessName,
            Email = request.Email,
            Phone = request.Phone,
            BillingAddress = request.BillingAddress,
            TaxId = request.TaxId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var dto = new CustomerDto
        {
            Id = customer.Id,
            Name = customer.Name,
            BusinessName = customer.BusinessName,
            Email = customer.Email,
            Phone = customer.Phone,
            BillingAddress = customer.BillingAddress,
            TaxId = customer.TaxId,
            IsActive = customer.IsActive,
            CreatedAt = customer.CreatedAt
        };

        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, [FromBody] UpdateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            ModelState.AddModelError(nameof(request.Name), "Name is required.");
            return ValidationProblem(ModelState);
        }

        var customer = await _db.Customers
            .Where(c => c.IsActive && c.Id == id)
            .FirstOrDefaultAsync();

        if (customer is null)
            return NotFound();

        customer.Name = request.Name.Trim();
        customer.BusinessName = request.BusinessName;
        customer.Email = request.Email;
        customer.Phone = request.Phone;
        customer.BillingAddress = request.BillingAddress;
        customer.TaxId = request.TaxId;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var customer = await _db.Customers
            .Where(c => c.IsActive && c.Id == id)
            .FirstOrDefaultAsync();

        if (customer is null)
            return NotFound();

        // Soft delete
        customer.IsActive = false;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
