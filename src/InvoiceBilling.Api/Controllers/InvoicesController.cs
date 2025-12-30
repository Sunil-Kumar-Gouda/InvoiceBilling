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
using InvoiceBilling.Domain.Services;
using Amazon.S3;
using Amazon.S3.Model;
using System.Net;

namespace InvoiceBilling.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly InvoiceBillingDbContext _db;
    private readonly IAmazonSQS _sqs;
    private readonly AwsOptions _aws;
    private readonly IInvoiceTotalsCalculator _totals;
    private readonly IAmazonS3 _s3;

    public InvoicesController(
        InvoiceBillingDbContext db,
        IAmazonSQS sqs,
        IAmazonS3 s3,
        IOptions<AwsOptions> awsOptions,
        IInvoiceTotalsCalculator totals)
    {
        _db = db;
        _sqs = sqs;
        _s3 = s3;
        _aws = awsOptions.Value;
        _totals = totals;
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

        invoice.TaxRatePercent = 0m;
        _totals.Apply(invoice);

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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> Put(Guid id, [FromBody] UpdateInvoiceRequest request)
    {
        // Load invoice with lines (tracked)
        var invoice = await _db.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice is null) return NotFound();

        // Draft-only rule
        if (!string.Equals(invoice.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            return Conflict("Only Draft invoices can be updated.");

        // Basic validations
        if (request.Lines is null || request.Lines.Count == 0)
            return BadRequest("At least one line is required.");

        if (request.DueDate == default)
            return BadRequest("DueDate is required.");

        if (request.DueDate.Date < invoice.IssueDate.Date)
            return BadRequest("DueDate cannot be before IssueDate.");

        // Update header fields
        invoice.DueDate = request.DueDate.Date;
        invoice.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? invoice.CurrencyCode
            : request.CurrencyCode.Trim().ToUpperInvariant();

        invoice.TaxRatePercent = request.TaxRatePercent;

        // Replace lines safely
        // IMPORTANT: remove old lines so EF deletes them
        if (invoice.Lines.Count > 0)
        {
            _db.InvoiceLines.RemoveRange(invoice.Lines);
            invoice.Lines.Clear();
        }

        foreach (var l in request.Lines)
        {
            if (l.ProductId == Guid.Empty) return BadRequest("Line ProductId is required.");
            if (string.IsNullOrWhiteSpace(l.Description)) return BadRequest("Line Description is required.");
            if (l.Quantity <= 0) return BadRequest("Line Quantity must be > 0.");
            if (l.UnitPrice < 0) return BadRequest("Line UnitPrice must be >= 0.");

            invoice.Lines.Add(new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                ProductId = l.ProductId,
                Description = l.Description.Trim(),
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity
                // LineTotal will be set by _totals.Apply(invoice)
            });
        }

        // Central totals calculation (updates LineTotal + totals)
        _totals.Apply(invoice);

        await _db.SaveChangesAsync();

        // Return updated DTO (include Lines if you want; below returns full DTO)
        return Ok(new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CustomerId = invoice.CustomerId,
            Status = invoice.Status,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            CurrencyCode = invoice.CurrencyCode,
            TaxRatePercent = invoice.TaxRatePercent,
            Subtotal = invoice.Subtotal,
            TaxTotal = invoice.TaxTotal,
            GrandTotal = invoice.GrandTotal,
            PdfS3Key = invoice.PdfS3Key,
            CreatedAt = invoice.CreatedAt,
            Lines = invoice.Lines.Select(x => new InvoiceLineDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                Description = x.Description,
                UnitPrice = x.UnitPrice,
                Quantity = x.Quantity,
                LineTotal = x.LineTotal
            }).ToList()
        });
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();

        if (string.IsNullOrWhiteSpace(invoice.PdfS3Key))
            return Conflict("Invoice file not generated yet. Please issue the invoice and wait for the worker.");

        var bucket = _aws.S3?.BucketName;
        if (string.IsNullOrWhiteSpace(bucket))
            return StatusCode(500, "AWS S3 bucket configuration missing (Aws:S3:BucketName).");

        try
        {
            var obj = await _s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucket,
                Key = invoice.PdfS3Key
            }, ct);

            // Decide content type based on key (works for .txt placeholder now and .pdf later)
            var contentType = GetContentTypeFromKey(invoice.PdfS3Key) ?? obj.Headers.ContentType ?? "application/octet-stream";

            // File name for download
            var ext = Path.GetExtension(invoice.PdfS3Key);
            var fileName = string.IsNullOrWhiteSpace(ext)
                ? $"{invoice.InvoiceNumber}"
                : $"{invoice.InvoiceNumber}{ext}";

            // Stream from S3 to client
            return File(obj.ResponseStream, contentType, fileName);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            return NotFound("Invoice file not found in S3. Re-issue the invoice or re-run the worker.");
        }
    }

    private static string? GetContentTypeFromKey(string key)
    {
        var ext = Path.GetExtension(key).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            _ => null
        };
    }
}
