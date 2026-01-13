using InvoiceBilling.Application.Common.Jobs;
using InvoiceBilling.Application.Common.Persistence;
using InvoiceBilling.Domain.Entities;
using InvoiceBilling.Domain.Exceptions;
using MediatR;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Application.Invoices.IssueInvoice;

public sealed class IssueInvoiceHandler
    : IRequestHandler<IssueInvoiceCommand, IssueInvoiceResponse>
{
    private readonly IInvoiceBillingDbContext _db;
    private readonly IInvoicePdfJobEnqueuer _pdfJobs;
    private readonly IValidator<IssueInvoiceCommand> _validator;

    public IssueInvoiceHandler(IInvoiceBillingDbContext db, IInvoicePdfJobEnqueuer pdfJobs, IValidator<IssueInvoiceCommand> validator)
    {
        _db = db;
        _pdfJobs = pdfJobs;
        _validator = validator;
    }

    public async Task<IssueInvoiceResponse> Handle(IssueInvoiceCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            var statusCode = MapValidationToStatus(validation.Errors);

            return new IssueInvoiceResponse(
                Succeeded: false,
                ErrorStatusCode: statusCode,
                ErrorTitle: statusCode == 404 ? "Invoice not found"
                          : statusCode == 409 ? "Invalid invoice state"
                          : "Validation failed",
                ErrorDetail: string.Join(Environment.NewLine, validation.Errors.Select(e => e.ErrorMessage)));
        }

        // Load with lines because the domain method validates presence of invoice lines.
        var invoice = await _db.Invoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            return new IssueInvoiceResponse(
                Succeeded: false,
                ErrorStatusCode: 404,
                ErrorTitle: "Invoice not found",
                ErrorDetail: $"Invoice {request.InvoiceId} was not found.");
        }

        var status = invoice.Status ?? string.Empty;
        var isDraft = string.Equals(status, InvoiceStatus.Draft, StringComparison.OrdinalIgnoreCase);
        var isIssued = string.Equals(status, InvoiceStatus.Issued, StringComparison.OrdinalIgnoreCase);

        // Guardrail (idempotency): repeated "issue" calls for an already-issued invoice are treated as a no-op success.
        // Optional self-heal: if PDF isn't attached yet, try enqueueing again (best-effort).
        if (isIssued)
        {
            var isJobEnqueued = false;
            string? jobEnqueueErrorMessage = null;
            if (string.IsNullOrWhiteSpace(invoice.PdfS3Key))
            {
                try
                {
                    await _pdfJobs.EnqueueInvoicePdfJobAsync(invoice.Id, cancellationToken);
                    isJobEnqueued = true;
                }
                catch (Exception ex)
                {
                    isJobEnqueued = false;
                    jobEnqueueErrorMessage = ex.Message;
                }
            }

            return new IssueInvoiceResponse(
                Succeeded: true,
                Invoice: invoice,
                JobEnqueued: isJobEnqueued,
                JobEnqueueError: jobEnqueueErrorMessage,
                WasNoOp: true);
        }

        if (!isDraft)
        {
            return new IssueInvoiceResponse(
                Succeeded: false,
                ErrorStatusCode: 409,
                ErrorTitle: "Invalid invoice state",
                ErrorDetail: $"Invoice is in '{status}' state. Only Draft invoices can be issued.");
        }

        try
        {
            invoice.Issue(request.IssuedAtUtc ?? DateTime.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            var statusCode = MapDomainExceptionToStatus(ex.Message);

            return new IssueInvoiceResponse(
                Succeeded: false,
                ErrorStatusCode: statusCode,
                ErrorTitle: "Domain rule violation",
                ErrorDetail: ex.Message);
        }

        var jobEnqueued = false;
        string? jobEnqueueError = null;

        try
        {
            await _pdfJobs.EnqueueInvoicePdfJobAsync(invoice.Id, cancellationToken);
            jobEnqueued = true;
        }
        catch (Exception ex)
        {
            // Important: invoice is already issued and saved. Do not fail the API call due to enqueue failure.
            jobEnqueued = false;
            jobEnqueueError = ex.Message;
        }

        return new IssueInvoiceResponse(
            Succeeded: true,
            Invoice: invoice,
            JobEnqueued: jobEnqueued,
            JobEnqueueError: jobEnqueueError);
    }

    private static int MapDomainExceptionToStatus(string message)
    {
        // Treat invalid state transitions as 409; input/invariant violations as 400
        if (message.Contains("Only Draft", StringComparison.OrdinalIgnoreCase)) return 409;
        if (message.Contains("already", StringComparison.OrdinalIgnoreCase)) return 409;
        return 400;
    }

    private static int MapValidationToStatus(IReadOnlyCollection<ValidationFailure> failures)
    {
        if (failures.Any(f => string.Equals(f.ErrorCode, "NOT_FOUND", StringComparison.OrdinalIgnoreCase)))
            return 404;

        if (failures.Any(f => string.Equals(f.ErrorCode, "CONFLICT", StringComparison.OrdinalIgnoreCase)))
            return 409;

        return 400;
    }
}

