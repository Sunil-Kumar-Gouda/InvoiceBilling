import { useEffect, useMemo, useState } from "react";
import type { ChangeEvent, FormEvent } from "react";

import type { Customer } from "../customers/types";
import type { Product } from "../products/types";
import type { CreateInvoiceRequest, InvoiceDto, InvoiceLine } from "./types";

import { getCustomers } from "../../api/customersApi";
import { getProducts } from "../../api/productsApi";
import { createInvoice, getInvoices, issueInvoice } from "../../api/invoicesApi";

type LineFormState = {
  productId: string;
  description: string;
  unitPrice: string; // keep as string in form
  quantity: string;  // keep as string in form
};

type InvoiceFormState = {
  customerId: string;
  issueDate: string; // yyyy-mm-dd
  dueDate: string;   // yyyy-mm-dd
  currencyCode: string;
  lines: LineFormState[];
};

function yyyyMmDd(d: Date): string {
  const yyyy = d.getFullYear();
  const mm = String(d.getMonth() + 1).padStart(2, "0");
  const dd = String(d.getDate()).padStart(2, "0");
  return `${yyyy}-${mm}-${dd}`;
}

function addDays(date: Date, days: number): Date {
  const copy = new Date(date);
  copy.setDate(copy.getDate() + days);
  return copy;
}

function safeCurrency(amount: number, currencyCode: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency: currencyCode }).format(amount);
  } catch {
    return `${currencyCode} ${amount.toFixed(2)}`;
  }
}

const emptyLine: LineFormState = {
  productId: "",
  description: "",
  unitPrice: "",
  quantity: "1",
};

function createEmptyForm(): InvoiceFormState {
  const today = new Date();
  return {
    customerId: "",
    issueDate: yyyyMmDd(today),
    dueDate: yyyyMmDd(addDays(today, 7)),
    currencyCode: "INR",
    lines: [{ ...emptyLine }],
  };
}

export default function InvoicesPage() {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [invoices, setInvoices] = useState<InvoiceDto[]>([]);

  const [loading, setLoading] = useState<boolean>(false);
  const [saving, setSaving] = useState<boolean>(false);
  const [issuingId, setIssuingId] = useState<string | null>(null);

  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState<InvoiceFormState>(() => createEmptyForm());

  const productsById = useMemo(() => {
    const map = new Map<string, Product>();
    for (const p of products) map.set(p.id, p);
    return map;
  }, [products]);

  const loadAll = async () => {
    try {
      setLoading(true);
      setError(null);

      const [c, p, inv] = await Promise.all([getCustomers(), getProducts(), getInvoices()]);
      setCustomers(c);
      setProducts(p);
      setInvoices(inv);

      // If currency is still default and we have products, keep INR; later you can auto align
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : "Failed to load invoices/customers/products";
      setError(message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleHeaderChange = (e: ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    setForm(prev => ({ ...prev, [name]: value }));
  };

  const addLine = () => {
    setForm(prev => ({ ...prev, lines: [...prev.lines, { ...emptyLine }] }));
  };

  const removeLine = (index: number) => {
    setForm(prev => {
      if (prev.lines.length <= 1) return prev;
      const next = prev.lines.filter((_, i) => i !== index);
      return { ...prev, lines: next };
    });
  };

  const updateLine = (index: number, patch: Partial<LineFormState>) => {
    setForm(prev => {
      const next = prev.lines.map((l, i) => (i === index ? { ...l, ...patch } : l));
      return { ...prev, lines: next };
    });
  };

  const onLineProductChange = (index: number, productId: string) => {
    const p = productsById.get(productId);
    // Basic autopopulate: description + unit price
    updateLine(index, {
      productId,
      description: p?.name ?? "",
      unitPrice: p ? String(p.unitPrice) : "",
    });

    // Optional: align currency to selected product’s currencyCode (if you want strict)
    if (p?.currencyCode) {
      setForm(prev => ({ ...prev, currencyCode: p.currencyCode }));
    }
  };

  const estimatedTotal = useMemo(() => {
    const currency = form.currencyCode || "INR";
    let subtotal = 0;

    for (const l of form.lines) {
      const qty = Number(l.quantity);
      const unit = Number(l.unitPrice);
      if (Number.isFinite(qty) && Number.isFinite(unit) && qty > 0 && unit >= 0) {
        subtotal += qty * unit;
      }
    }

    return { currency, subtotal };
  }, [form.currencyCode, form.lines]);

  const resetForm = () => {
    setForm(createEmptyForm());
  };

  const validateAndBuildRequest = (): CreateInvoiceRequest | null => {
    if (!form.customerId) {
      setError("Customer is required.");
      return null;
    }

    if (!form.issueDate || !form.dueDate) {
      setError("Issue Date and Due Date are required.");
      return null;
    }

    if (!form.lines || form.lines.length === 0) {
      setError("At least one line is required.");
      return null;
    }

    const lines: InvoiceLine[] = [];
    for (let i = 0; i < form.lines.length; i++) {
      const l = form.lines[i];

      if (!l.productId) {
        setError(`Line ${i + 1}: Product is required.`);
        return null;
      }
      const desc = l.description.trim();
      if (!desc) {
        setError(`Line ${i + 1}: Description is required.`);
        return null;
      }

      const unitPrice = Number(l.unitPrice);
      const quantity = Number(l.quantity);

      if (!Number.isFinite(unitPrice) || unitPrice < 0) {
        setError(`Line ${i + 1}: Unit Price must be a valid number (>= 0).`);
        return null;
      }
      if (!Number.isFinite(quantity) || quantity <= 0) {
        setError(`Line ${i + 1}: Quantity must be a valid number (> 0).`);
        return null;
      }

      lines.push({
        productId: l.productId,
        description: desc,
        unitPrice,
        quantity,
      });
    }

    const req: CreateInvoiceRequest = {
      customerId: form.customerId,
      issueDate: form.issueDate,
      dueDate: form.dueDate,
      currencyCode: (form.currencyCode || "INR").trim().toUpperCase(),
      lines,
    };

    return req;
  };

  const handleCreate = async (e: FormEvent) => {
    e.preventDefault();

    const req = validateAndBuildRequest();
    if (!req) return;

    try {
      setSaving(true);
      setError(null);

      const created = await createInvoice(req);

      // Update list immediately; server remains source of truth (you can also reload)
      setInvoices(prev => [created, ...prev]);
      resetForm();
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : "Failed to create invoice";
      setError(message);
    } finally {
      setSaving(false);
    }
  };

  const handleIssue = async (invoiceId: string) => {
    try {
      setIssuingId(invoiceId);
      setError(null);

      await issueInvoice(invoiceId);

      // Refresh to get updated status + PdfS3Key once worker processes it
      await loadAll();
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : "Failed to issue invoice";
      setError(message);
    } finally {
      setIssuingId(null);
    }
  };

  return (
    <div style={{ padding: 16, maxWidth: 1200, margin: "0 auto" }}>
      <h2 style={{ marginBottom: 8 }}>Invoices</h2>

      <div style={{ display: "flex", gap: 8, alignItems: "center", marginBottom: 12 }}>
        <button type="button" onClick={loadAll} disabled={loading}>
          Refresh
        </button>
        {loading && <span>Loading...</span>}
      </div>

      {error && (
        <div style={{ marginBottom: 12, padding: 10, border: "1px solid #f5c2c7", background: "#f8d7da" }}>
          <strong>Error:</strong> {error}
        </div>
      )}

      <form onSubmit={handleCreate} style={{ marginBottom: 16, padding: 12, border: "1px solid #ddd" }}>
        <h3 style={{ marginTop: 0 }}>Create Invoice</h3>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr 1fr", gap: 12 }}>
          <div>
            <label style={{ display: "block", marginBottom: 6 }}>
              Customer <span style={{ color: "crimson" }}>*</span>
            </label>
            <select
              name="customerId"
              value={form.customerId}
              onChange={handleHeaderChange}
              style={{ width: "100%", padding: 8 }}
            >
              <option value="">-- Select customer --</option>
              {customers.map(c => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label style={{ display: "block", marginBottom: 6 }}>
              Issue Date <span style={{ color: "crimson" }}>*</span>
            </label>
            <input
              type="date"
              name="issueDate"
              value={form.issueDate}
              onChange={handleHeaderChange}
              style={{ width: "100%", padding: 8 }}
            />
          </div>

          <div>
            <label style={{ display: "block", marginBottom: 6 }}>
              Due Date <span style={{ color: "crimson" }}>*</span>
            </label>
            <input
              type="date"
              name="dueDate"
              value={form.dueDate}
              onChange={handleHeaderChange}
              style={{ width: "100%", padding: 8 }}
            />
          </div>

          <div>
            <label style={{ display: "block", marginBottom: 6 }}>Currency</label>
            <input
              name="currencyCode"
              value={form.currencyCode}
              onChange={handleHeaderChange}
              placeholder="INR"
              style={{ width: "100%", padding: 8 }}
            />
          </div>
        </div>

        <div style={{ marginTop: 12, marginBottom: 8 }}>
          <strong>Lines</strong>
        </div>

        <div style={{ display: "grid", gap: 10 }}>
          {form.lines.map((l, idx) => (
            <div key={idx} style={{ border: "1px solid #eee", padding: 10 }}>
              <div style={{ display: "grid", gridTemplateColumns: "2fr 2fr 1fr 1fr auto", gap: 10 }}>
                <div>
                  <label style={{ display: "block", marginBottom: 6 }}>
                    Product <span style={{ color: "crimson" }}>*</span>
                  </label>
                  <select
                    value={l.productId}
                    onChange={(e) => onLineProductChange(idx, e.target.value)}
                    style={{ width: "100%", padding: 8 }}
                  >
                    <option value="">-- Select product --</option>
                    {products.map(p => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label style={{ display: "block", marginBottom: 6 }}>
                    Description <span style={{ color: "crimson" }}>*</span>
                  </label>
                  <input
                    value={l.description}
                    onChange={(e) => updateLine(idx, { description: e.target.value })}
                    placeholder="Line description"
                    style={{ width: "100%", padding: 8 }}
                  />
                </div>

                <div>
                  <label style={{ display: "block", marginBottom: 6 }}>
                    Unit Price <span style={{ color: "crimson" }}>*</span>
                  </label>
                  <input
                    inputMode="decimal"
                    value={l.unitPrice}
                    onChange={(e) => updateLine(idx, { unitPrice: e.target.value })}
                    placeholder="0.00"
                    style={{ width: "100%", padding: 8 }}
                  />
                </div>

                <div>
                  <label style={{ display: "block", marginBottom: 6 }}>
                    Qty <span style={{ color: "crimson" }}>*</span>
                  </label>
                  <input
                    inputMode="decimal"
                    value={l.quantity}
                    onChange={(e) => updateLine(idx, { quantity: e.target.value })}
                    placeholder="1"
                    style={{ width: "100%", padding: 8 }}
                  />
                </div>

                <div style={{ display: "flex", alignItems: "end" }}>
                  <button type="button" onClick={() => removeLine(idx)} disabled={form.lines.length <= 1}>
                    Remove
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>

        <div style={{ marginTop: 10, display: "flex", gap: 8, alignItems: "center" }}>
          <button type="button" onClick={addLine}>
            + Add line
          </button>

          <div style={{ marginLeft: "auto" }}>
            <strong>Estimated subtotal:</strong>{" "}
            {safeCurrency(estimatedTotal.subtotal, estimatedTotal.currency)}
          </div>
        </div>

        <div style={{ marginTop: 12, display: "flex", gap: 8 }}>
          <button type="submit" disabled={saving}>
            {saving ? "Saving..." : "Create Invoice"}
          </button>
          <button type="button" onClick={resetForm} disabled={saving}>
            Reset
          </button>
        </div>
      </form>

      <h3 style={{ marginTop: 0 }}>Invoices List</h3>

      {invoices.length === 0 ? (
        <p>No invoices found.</p>
      ) : (
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>Number</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>Status</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>Issue</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>Due</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>Total</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>S3 Key</th>
                <th style={{ borderBottom: "1px solid #ccc", padding: 8 }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {invoices.map(inv => (
                <tr key={inv.id}>
                  <td style={{ borderBottom: "1px solid #eee", padding: 8 }}>{inv.invoiceNumber}</td>
                  <td style={{ borderBottom: "1px solid #eee", padding: 8 }}>{inv.status}</td>
                  <td style={{ borderBottom: "1px solid #eee", padding: 8 }}>
                    {inv.issueDate ? new Date(inv.issueDate).toLocaleDateString() : ""}
                  </td>
                  <td style={{ borderBottom: "1px solid #eee", padding: 8 }}>
                    {inv.dueDate ? new Date(inv.dueDate).toLocaleDateString() : ""}
                  </td>
                  <td style={{ borderBottom: "1px solid #eee", padding: 8 }}>
                    {safeCurrency(inv.grandTotal, inv.currencyCode)}
                  </td>
                  <td style={{ borderBottom: "1px solid #eee", padding: 8 }}>
                    {inv.pdfS3Key ?? ""}
                  </td>
                  <td style={{ borderBottom: "1px solid #eee", padding: 8, whiteSpace: "nowrap" }}>
                    <button
                      type="button"
                      onClick={() => handleIssue(inv.id)}
                      disabled={inv.status === "Issued" || issuingId === inv.id}
                    >
                      {issuingId === inv.id ? "Issuing..." : "Issue"}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <p style={{ marginTop: 8 }}>
            Note: after issuing, the S3 key will appear once the background worker processes the SQS message.
          </p>
        </div>
      )}
    </div>
  );
}
