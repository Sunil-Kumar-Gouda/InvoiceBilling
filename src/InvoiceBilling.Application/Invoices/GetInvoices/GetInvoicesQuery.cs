using MediatR;

namespace InvoiceBilling.Application.Invoices.GetInvoices;

/// <summary>
/// CQRS query: List invoices with basic filters + paging.
/// </summary>
public sealed record GetInvoicesQuery(
    string? Status,
    Guid? CustomerId,
    DateTime? IssueDateFrom,
    DateTime? IssueDateTo,
    int Page = 1,
    int PageSize = 50
) : IRequest<GetInvoicesResponse>;

public sealed record GetInvoicesResponse(
    bool Succeeded,
    IReadOnlyList<InvoiceListItem> Items,
    int Page,
    int PageSize,
    int? ErrorStatusCode = null,
    string? ErrorTitle = null,
    string? ErrorDetail = null
);

public sealed record InvoiceListItem(
    Guid Id,
    string InvoiceNumber,
    Guid CustomerId,
    string Status,
    DateTime IssueDate,
    DateTime DueDate,
    string CurrencyCode,
    decimal TaxRatePercent,
    decimal Subtotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal PaidTotal,
    decimal BalanceDue,
    string? PdfS3Key,
    DateTime CreatedAt
);
