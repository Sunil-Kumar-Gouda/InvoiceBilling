using InvoiceBilling.Api.Dtos.Invoices;
using InvoiceBilling.Api.Dtos.Payments;
using InvoiceBilling.Application.Invoices.CreateInvoice;
using InvoiceBilling.Application.Invoices.GetInvoiceById;
using InvoiceBilling.Application.Invoices.GetInvoicePayments;
using InvoiceBilling.Application.Invoices.GetInvoicePdf;
using InvoiceBilling.Application.Invoices.GetInvoices;
using InvoiceBilling.Application.Invoices.GetInvoiceStatus;
using InvoiceBilling.Application.Invoices.IssueInvoice;
using InvoiceBilling.Application.Invoices.RecordPayment;
using InvoiceBilling.Application.Invoices.UpdateDraftInvoice;
using InvoiceBilling.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceBilling.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(IMediator mediator, ILogger<InvoicesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    // List invoices with basic filters + paging
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
        var cmd = new CreateInvoiceCommand(
            CustomerId: request.CustomerId,
            IssueDate: request.IssueDate,
            DueDate: request.DueDate,
            CurrencyCode: request.CurrencyCode,
            Lines: (request.Lines ?? new List<CreateInvoiceLineRequest>())
                .Select(l => new CreateInvoiceLine(l.ProductId, l.Description, l.UnitPrice, l.Quantity))
                .ToList());

        var result = await _mediator.Send(cmd, ct);

        if (!result.Succeeded)
        {
            return Problem(
                title: result.ErrorTitle ?? "Request failed",
                detail: result.ErrorDetail ?? "The request could not be completed.",
                statusCode: result.ErrorStatusCode ?? StatusCodes.Status400BadRequest);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Invoice!.Id }, ToDto(result.Invoice));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> Put(Guid id, [FromBody] UpdateInvoiceRequest request, CancellationToken ct)
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

    [HttpGet("{id:guid}/payments")]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> GetPayments(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetInvoicePaymentsQuery(id), ct);

        if (!result.Succeeded)
        {
            return Problem(
                title: result.ErrorTitle ?? "Request failed",
                detail: result.ErrorDetail ?? "The request could not be completed.",
                statusCode: result.ErrorStatusCode ?? StatusCodes.Status400BadRequest);
        }

        var items = result.Payments!.Select(p => new PaymentDto
        {
            Id = p.Id,
            InvoiceId = p.InvoiceId,
            Amount = p.Amount,
            PaidAt = p.PaidAt,
            Method = p.Method,
            Reference = p.Reference,
            Note = p.Note,
            CreatedAt = p.CreatedAt
        }).ToList();

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
        var result = await _mediator.Send(new GetInvoicePdfQuery(id), ct);

        if (!result.Succeeded)
        {
            return Problem(
                title: result.ErrorTitle ?? "Request failed",
                detail: result.ErrorDetail ?? "The request could not be completed.",
                statusCode: result.ErrorStatusCode ?? StatusCodes.Status400BadRequest);
        }

        return File(result.ContentStream!, result.ContentType!, result.FileName);
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
}
