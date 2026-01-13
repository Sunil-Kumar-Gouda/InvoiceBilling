using FluentValidation;
using FluentValidation.Results;
using InvoiceBilling.Application.Common.Persistence;
using InvoiceBilling.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBilling.Application.Invoices.IssueInvoice;

/// <summary>
/// Enterprise validation for issuing an invoice.
///
/// Rules:
/// - InvoiceId must be provided.
/// - Invoice must exist (404).
/// - Invoice must be Draft (409).
/// - Invoice must have at least one line (400).
///
/// Note: DB-backed checks are intentionally performed here to provide early,
/// client-friendly errors before attempting the state transition.
/// </summary>
public sealed class IssueInvoiceCommandValidator : AbstractValidator<IssueInvoiceCommand>
{
    private readonly IInvoiceBillingDbContext _db;

    public IssueInvoiceCommandValidator(IInvoiceBillingDbContext db)
    {
        _db = db;

        RuleFor(x => x.InvoiceId)
            .NotEmpty()
            .WithMessage("InvoiceId is required.");

        // Optional field: if provided, it must be a valid non-default timestamp.
        RuleFor(x => x.IssuedAtUtc)
            .Must(d => d is null || d.Value != default)
            .WithMessage("IssuedAtUtc must be a valid timestamp.");

        RuleFor(x => x)
            .CustomAsync(ValidateInvoiceStateAsync);
    }

    private async Task ValidateInvoiceStateAsync(IssueInvoiceCommand cmd, ValidationContext<IssueInvoiceCommand> ctx, CancellationToken ct)
    {
        if (cmd.InvoiceId == Guid.Empty)
            return;

        // Single round-trip snapshot: status + line count.
        var snapshot = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.Id == cmd.InvoiceId)
            .Select(i => new
            {
                i.Status,
                LineCount = i.Lines.Count
            })
            .FirstOrDefaultAsync(ct);

        if (snapshot is null)
        {
            ctx.AddFailure(new ValidationFailure(nameof(cmd.InvoiceId), $"Invoice {cmd.InvoiceId} was not found.")
            {
                ErrorCode = "NOT_FOUND"
            });

            return;
        }

        var status = snapshot.Status ?? string.Empty;
        var isDraft = string.Equals(status, InvoiceStatus.Draft, StringComparison.OrdinalIgnoreCase);
        var isIssued = string.Equals(status, InvoiceStatus.Issued, StringComparison.OrdinalIgnoreCase);

        // Guardrail (idempotency): "issue" is treated as idempotent for already-issued invoices.
        // For any other non-draft status, return a conflict.
        if (!isDraft && !isIssued)
        {
            ctx.AddFailure(new ValidationFailure(nameof(cmd.InvoiceId), $"Invoice is in '{status}' state. Only Draft invoices can be issued.")
            {
                ErrorCode = "CONFLICT"
            });
        }

        if (snapshot.LineCount <= 0)
        {
            if (isDraft)
            {
                ctx.AddFailure(new ValidationFailure(nameof(cmd.InvoiceId), "Cannot issue an invoice without lines.")
                {
                    ErrorCode = "VALIDATION"
                });
            }
            else if (isIssued)
            {
                // Defensive: should not happen in normal flows, but better to surface as a conflict.
                ctx.AddFailure(new ValidationFailure(nameof(cmd.InvoiceId), "Issued invoice has no lines (data is inconsistent).")
                {
                    ErrorCode = "CONFLICT"
                });
            }
        }
    }
}
