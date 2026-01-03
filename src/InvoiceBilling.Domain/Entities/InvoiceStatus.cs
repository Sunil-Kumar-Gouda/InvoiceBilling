namespace InvoiceBilling.Domain.Entities;

public static class InvoiceStatus
{
    public const string Draft = "Draft";
    public const string Issued = "Issued";
    public const string Paid = "Paid";
    public const string Void = "Void";

    public static bool IsKnown(string? status) =>
        status is Draft or Issued or Paid or Void;
}
