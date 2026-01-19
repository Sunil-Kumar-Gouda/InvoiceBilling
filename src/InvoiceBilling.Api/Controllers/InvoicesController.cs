using Amazon.S3;
using Amazon.S3.Model;
using InvoiceBilling.Api.Dtos.Invoices;
using InvoiceBilling.Api.Dtos.Payments;
using InvoiceBilling.Application.Invoices.GetInvoiceById;
using InvoiceBilling.Application.Invoices.GetInvoices;
using InvoiceBilling.Application.Invoices.GetInvoiceStatus;
using InvoiceBilling.Application.Invoices.IssueInvoice;
using InvoiceBilling.Application.Invoices.RecordPayment;
using InvoiceBilling.Application.Invoices.UpdateDraftInvoice;
using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Domain.Exceptions;
using InvoiceBilling.Domain.Services;
using InvoiceBilling.Infrastructure.Cloud;
using InvoiceBilling.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;

namespace InvoiceBilling.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly InvoiceBillingDbContext _db;
    private readonly IMediator _mediator;
    private readonly AwsOptions _aws;
    private readonly IInvoiceTotalsCalculator _totals;
    private readonly IAmazonS3 _s3;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(
        InvoiceBillingDbContext db,
        IMediator mediator,
        IAmazonS3 s3,
        IOptions<AwsOptions> awsOptions,
        IInvoiceTotalsCalculator totals,
        ILogger<InvoicesController> logger)
    {
        _db = db;
        _mediator = mediator;
        _s3 = s3;
        _aws = awsOptions.Value;
        _totals = totals;
        _logger = logger;
    }

    // Day 8 (Phase 2): List invoices with basic filters + paging
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvoiceDto>>> Get(
        [FromQuery] string? status,
        [FromQuery] Guid? customerId,
        [FromQuery] DateTime? issueDateFrom,
        [FromQuery] DateTime? issueDateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetInvoicesQuery(
                Status: status,
                CustomerId: customerId,
                IssueDateFrom: issueDateFrom,
                IssueDateTo: issueDateTo,
                Page: page,
                PageSize: pageSize),
            ct);

        if (!result.Succeeded)
        {
            return Problem(
                title: result.ErrorTitle ?? "Request failed",
                detail: result.ErrorDetail ?? "The request could not be completed.",
                statusCode: result.ErrorStatusCode ?? StatusCodes.Status400BadRequest);
        }

        var items = result.Items.Select(ToDto).ToList();
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetInvoiceByIdQuery(id), ct);

        if (!result.Succeeded)
        {
            return Problem(
                title: result.ErrorTitle ?? "Request failed",
                detail: result.ErrorDetail ?? "The request could not be completed.",
                statusCode: result.ErrorStatusCode ?? StatusCodes.Status400BadRequest);
        }

        return Ok(ToDto(result.Invoice!));
    }

    /// <summary>
    /// Lightweight endpoint for UI polling (status + PDF readiness) without loading lines.
    /// </summary>
    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<InvoiceStatusDto>> GetStatus(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetInvoiceStatusQuery(id), ct);

        if (!result.Succeeded)
        {
            return Problem(
                title: result.ErrorTitle ?? "Request failed",
                detail: result.ErrorDetail ?? "The request could not be completed.",
                statusCode: result.ErrorStatusCode ?? StatusCodes.Status400BadRequest);
        }

        var state = result.State!;

        var isIssuedOrPaid = string.Equals(state.RawStatus, InvoiceStatus.Issued, StringComparison.OrdinalIgnoreCase)
                          || string.Equals(state.RawStatus, InvoiceStatus.Paid, StringComparison.OrdinalIgnoreCase);

        var pdfStatus = !isIssuedOrPaid
            ? "NotIssued"
            : (string.IsNullOrWhiteSpace(state.PdfS3Key) ? "Pending" : "Ready");

        var pdfUrl = pdfStatus == "Ready"
            ? Url.Action(nameof(DownloadPdf), new { id = state.Id })
            : null;

        return Ok(new InvoiceStatusDto
        {
            Id = state.Id,
            Status = state.EffectiveStatus,
            PaidTotal = state.PaidTotal,
            BalanceDue = state.BalanceDue,
            PdfStatus = pdfStatus,
            PdfDownloadUrl = pdfUrl
        });
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceDto>> Post([FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        if (request.CustomerId == Guid.Empty)
            return Problem(title: "Validation failed", detail: "CustomerId is required.", statusCode: 400);

        if (request.Lines is null || request.Lines.Count == 0)
            return Problem(title: "Validation failed", detail: "At least one line is required.", statusCode: 400);

        // DB-level existence checks (domain does not query DB)
        var customerExists = await _db.Customers.AsNoTracking()
            .AnyAsync(c => c.Id == request.CustomerId, ct);

        if (!customerExists)
            return Problem(title: "Validation failed", detail: $"Unknown CustomerId: {request.CustomerId}", statusCode: 400);

        var productIds = request.Lines.Select(x => x.ProductId).Where(x => x != Guid.Empty).Distinct().ToArray();
        var existingProductIds = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);

        var missing = productIds.Except(existingProductIds).ToArray();
        if (missing.Length > 0)
            return Problem(title: "Validation failed", detail: $"Unknown ProductId(s): {string.Join(", ", missing)}", statusCode: 400);

        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Random.Shared.Next(1000, 9999)}";
        var issueDate = request.IssueDate == default ? DateTime.UtcNow.Date : request.IssueDate.Date;
        var dueDate = request.DueDate == default ? issueDate.AddDays(7) : request.DueDate.Date;

        var lines = request.Lines.Select(l => (l.ProductId, l.Description, l.UnitPrice, l.Quantity));

        try
        {
            var invoice = Invoice.CreateDraft(
                id: Guid.NewGuid(),
                invoiceNumber: invoiceNumber,
                customerId: request.CustomerId,
                issueDate: issueDate,
                dueDate: dueDate,
                currencyCode: request.CurrencyCode,
                createdAtUtc: DateTime.UtcNow,
                lines: lines);

            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, ToDto(invoice));
        }
        catch (DomainException ex)
        {
            var status = MapDomainExceptionToStatus(ex.Message);
            return DomainProblem("Domain rule violation", ex.Message, status);
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> Put(Guid id, [FromBody] UpdateInvoiceRequest request, CancellationToken ct)
    {
        try
        {
            var cmd = new UpdateDraftInvoiceCommand(
                InvoiceId: id,
                DueDate: request.DueDate,
                CurrencyCode: request.CurrencyCode,
                TaxRatePercent: request.TaxRatePercent,
                Lines: (request.Lines ?? new List<UpdateInvoiceLineRequest>())
                    .Select(l => new UpdateDraftInvoiceLine(l.ProductId, l.Description, l.UnitPrice, l.Quantity))
                    .ToList());

            var result = await _mediator.Send(cmd, ct);

            if (!result.Succeeded)
            {
                return Problem(
                    title: result.ErrorTitle ?? "Request failed",
                    detail: result.ErrorDetail ?? "The request could not be completed.",
                    statusCode: result.ErrorStatusCode ?? StatusCodes.Status400BadRequest);
            }

            return Ok(ToDto(result.Invoice!));
        }
        catch (DomainException ex)
        {
            var status = MapDomainExceptionToStatus(ex.Message);
            return DomainProblem("Domain rule violation", ex.Message, status);
        }
    }

    [HttpPost("{id:guid}/issue")]
    public async Task<IActionResult> Issue(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new IssueInvoiceCommand(InvoiceId: id), ct);

        if (!result.Succeeded)
        {
            return Problem(
                title: result.ErrorTitle ?? "Request failed",
                detail: result.ErrorDetail ?? "The request could not be completed.",
                statusCode: result.ErrorStatusCode ?? StatusCodes.Status400BadRequest);
        }

        var message = result.WasNoOp
            ? (result.JobEnqueued
                ? "Invoice already issued. Job enqueued."
                : "Invoice already issued.")
            : (result.JobEnqueued
                ? "Invoice issued and job enqueued."
                : "Invoice issued. PDF job enqueue failed or is disabled.");

        return Ok(new
        {
            message,
            invoiceId = id,
            jobEnqueued = result.JobEnqueued,
            jobEnqueueError = result.JobEnqueueError,
            wasNoOp = result.WasNoOp,
            invoice = ToDto(result.Invoice!)
        });
    }

    // Day 13: Payments
    [HttpGet("{id:guid}/payments")]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> GetPayments(Guid id, CancellationToken ct)
    {
        var exists = await _db.Invoices.AsNoTracking().AnyAsync(i => i.Id == id, ct);
        if (!exists)
            return Problem(title: "Invoice not found", detail: $"Invoice {id} was not found.", statusCode: 404);

        var items = await _db.Payments.AsNoTracking()
            .Where(p => p.InvoiceId == id)
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new PaymentDto
            {
                Id = p.Id,
                InvoiceId = p.InvoiceId,
                Amount = p.Amount,
                PaidAt = p.PaidAt,
                Method = p.Method,
                Reference = p.Reference,
                Note = p.Note,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordPaymentRequest request, CancellationToken ct)
    {
        var cmd = new RecordPaymentCommand(
            InvoiceId: id,
            Amount: request.Amount,
            PaidAtUtc: request.PaidAtUtc,
            Method: request.Method,
            Reference: request.Reference,
            Note: request.Note);

        var result = await _mediator.Send(cmd, ct);

        if (!result.Succeeded)
        {
            return Problem(
                title: result.ErrorTitle ?? "Request failed",
                detail: result.ErrorDetail ?? "The request could not be completed.",
                statusCode: result.ErrorStatusCode ?? StatusCodes.Status400BadRequest);
        }

        return Ok(new
        {
            message = "Payment recorded.",
            invoice = ToDto(result.Invoice!),
            payment = new PaymentDto
            {
                Id = result.Payment!.Id,
                InvoiceId = result.Payment.InvoiceId,
                Amount = result.Payment.Amount,
                PaidAt = result.Payment.PaidAt,
                Method = result.Payment.Method,
                Reference = result.Payment.Reference,
                Note = result.Payment.Note,
                CreatedAt = result.Payment.CreatedAt
            }
        });
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();

        if (string.IsNullOrWhiteSpace(invoice.PdfS3Key))
            return Problem409("PDF not ready",
                "Invoice file not generated yet. Please issue the invoice and wait for the worker.");

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

            var contentType = GetContentTypeFromKey(invoice.PdfS3Key) ?? obj.Headers.ContentType ?? "application/octet-stream";

            var ext = Path.GetExtension(invoice.PdfS3Key);
            var fileName = string.IsNullOrWhiteSpace(ext)
                ? $"{invoice.InvoiceNumber}"
                : $"{invoice.InvoiceNumber}{ext}";

            return File(obj.ResponseStream, contentType, fileName);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey")
        {
            return Problem404("PDF not found", "Invoice file not found in S3. Re-issue the invoice or re-run the worker.");
        }
    }

    private static InvoiceDto ToDto(Invoice invoice)
    {
        var today = DateTime.UtcNow.Date;
        var effectiveStatus = (invoice.Status == InvoiceStatus.Issued && invoice.DueDate < today && invoice.BalanceDue > 0)
            ? InvoiceStatus.Overdue
            : invoice.Status;

        return new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            CustomerId = invoice.CustomerId,
            Status = effectiveStatus,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            CurrencyCode = invoice.CurrencyCode,
            TaxRatePercent = invoice.TaxRatePercent,
            Subtotal = invoice.Subtotal,
            TaxTotal = invoice.TaxTotal,
            GrandTotal = invoice.GrandTotal,
            PaidTotal = invoice.PaidTotal,
            BalanceDue = invoice.BalanceDue,
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
        };
    }

    private static InvoiceDto ToDto(InvoiceListItem invoice)
    {
        return new InvoiceDto
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
            PaidTotal = invoice.PaidTotal,
            BalanceDue = invoice.BalanceDue,
            PdfS3Key = invoice.PdfS3Key,
            CreatedAt = invoice.CreatedAt
        };
    }

    private static InvoiceDto ToDto(InvoiceDetails invoice)
    {
        return new InvoiceDto
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
            PaidTotal = invoice.PaidTotal,
            BalanceDue = invoice.BalanceDue,
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
        };
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

    private IActionResult Problem409(string title, string detail) =>
        Problem(title: title, detail: detail, statusCode: StatusCodes.Status409Conflict);

    private IActionResult Problem404(string title, string detail) =>
        Problem(title: title, detail: detail, statusCode: StatusCodes.Status404NotFound);

    private ObjectResult DomainProblem(string title, string detail, int statusCode) =>
        Problem(title: title, detail: detail, statusCode: statusCode);

    private int MapDomainExceptionToStatus(string message)
    {
        // Treat invalid state transitions as 409; input/invariant violations as 400
        if (message.Contains("Only Draft", StringComparison.OrdinalIgnoreCase)) return StatusCodes.Status409Conflict;
        if (message.Contains("already", StringComparison.OrdinalIgnoreCase)) return StatusCodes.Status409Conflict;
        return StatusCodes.Status400BadRequest;
    }
}
