using Amazon.SQS;
using Amazon.SQS.Model;
using InvoiceBilling.Api.Dtos.Invoices;
using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Infrastructure.Cloud;
using InvoiceBilling.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace InvoiceBilling.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly InvoiceBillingDbContext _db;
    private readonly IAmazonSQS _sqs;
    private readonly AwsOptions _aws;

    public InvoicesController(
        InvoiceBillingDbContext db,
        IAmazonSQS sqs,
        IOptions<AwsOptions> awsOptions)
    {
        _db = db;
        _sqs = sqs;
        _aws = awsOptions.Value;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceDto>>> Get()
    {
        var items = await _db.Invoices.AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvoiceDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                CustomerId = i.CustomerId,
                Status = i.Status,
                IssueDate = i.IssueDate,
                DueDate = i.DueDate,
                CurrencyCode = i.CurrencyCode,
                Subtotal = i.Subtotal,
                TaxTotal = i.TaxTotal,
                GrandTotal = i.GrandTotal,
                PdfS3Key = i.PdfS3Key,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> GetById(Guid id)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice is null) return NotFound();

        return Ok(new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CustomerId = invoice.CustomerId,
            Status = invoice.Status,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            CurrencyCode = invoice.CurrencyCode,
            Subtotal = invoice.Subtotal,
            TaxTotal = invoice.TaxTotal,
            GrandTotal = invoice.GrandTotal,
            PdfS3Key = invoice.PdfS3Key,
            CreatedAt = invoice.CreatedAt,
            Lines = invoice.Lines.Select(l => new InvoiceLineDto
            {
                Id = l.Id,
                ProductId = l.ProductId,
                Description = l.Description,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity,
                LineTotal = l.LineTotal
            }).ToList()
        });
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceDto>> Post([FromBody] CreateInvoiceRequest request)
    {
        if (request.CustomerId == Guid.Empty)
            return BadRequest("CustomerId is required.");

        if (request.Lines is null || request.Lines.Count == 0)
            return BadRequest("At least one line is required.");

        // Invoice number: simple, deterministic format for now
        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = invoiceNumber,
            CustomerId = request.CustomerId,
            Status = "Draft",
            IssueDate = request.IssueDate == default ? DateTime.UtcNow.Date : request.IssueDate.Date,
            DueDate = request.DueDate == default ? DateTime.UtcNow.Date.AddDays(7) : request.DueDate.Date,
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "INR" : request.CurrencyCode.Trim().ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow
        };

        foreach (var l in request.Lines)
        {
            if (l.ProductId == Guid.Empty) return BadRequest("Line ProductId is required.");
            if (string.IsNullOrWhiteSpace(l.Description)) return BadRequest("Line Description is required.");
            if (l.Quantity <= 0) return BadRequest("Line Quantity must be > 0.");
            if (l.UnitPrice < 0) return BadRequest("Line UnitPrice must be >= 0.");

            var lineTotal = l.UnitPrice * l.Quantity;

            invoice.Lines.Add(new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                ProductId = l.ProductId,
                Description = l.Description.Trim(),
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity,
                LineTotal = lineTotal
            });
        }

        invoice.Subtotal = invoice.Lines.Sum(x => x.LineTotal);
        invoice.TaxTotal = 0m; // Day 6 will add tax rules
        invoice.GrandTotal = invoice.Subtotal + invoice.TaxTotal;

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CustomerId = invoice.CustomerId,
            Status = invoice.Status,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            CurrencyCode = invoice.CurrencyCode,
            Subtotal = invoice.Subtotal,
            TaxTotal = invoice.TaxTotal,
            GrandTotal = invoice.GrandTotal,
            PdfS3Key = invoice.PdfS3Key,
            CreatedAt = invoice.CreatedAt,
            Lines = invoice.Lines.Select(l => new InvoiceLineDto
            {
                Id = l.Id,
                ProductId = l.ProductId,
                Description = l.Description,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity,
                LineTotal = l.LineTotal
            }).ToList()
        });
    }

    [HttpPost("{id:guid}/issue")]
    public async Task<IActionResult> Issue(Guid id)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null) return NotFound();

        if (invoice.Status == "Issued")
            return Conflict("Invoice already issued.");

        invoice.Status = "Issued";
        invoice.IssueDate = DateTime.UtcNow.Date;

        await _db.SaveChangesAsync();

        // Enqueue PDF generation job (LocalStack SQS)
        var queueUrl = (await _sqs.GetQueueUrlAsync(_aws.Sqs.QueueName)).QueueUrl;

        var payload = JsonSerializer.Serialize(new { invoiceId = id });
        await _sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = queueUrl,
            MessageBody = payload
        });

        return Ok(new { message = "Invoice issued and job enqueued.", invoiceId = id });
    }
}
