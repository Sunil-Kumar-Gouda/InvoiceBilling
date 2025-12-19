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
    public async Task<ActionResult<IEnumerable<CustomerDto>>> Get()
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

        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }
}
