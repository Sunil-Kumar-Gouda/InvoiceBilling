import { useEffect, useMemo, useState } from "react";
import type { ChangeEvent, FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";

import type { Product } from "../products/types";
import type { InvoiceDto, UpdateInvoiceRequest } from "./types";

import { getProducts } from "../../api/productsApi";
import { getInvoiceById, updateInvoice } from "../../api/invoicesApi";
import { ApiError } from "../../api/types";

type LineFormState = {
  productId: string;
  description: string;
  unitPrice: string;
  quantity: string;
};

type FormState = {
  dueDate: string;
  currencyCode: string;
  taxRatePercent: string;
  lines: LineFormState[];
};

const emptyLine: LineFormState = { productId: "", description: "", unitPrice: "", quantity: "1" };

function fmtYmd(iso: string): string {
  return iso?.length >= 10 ? iso.substring(0, 10) : iso;
}

export default function InvoiceEditPage() {
  const { id } = useParams();
  const invoiceId = id ?? "";
  const navigate = useNavigate();

  const [invoice, setInvoice] = useState<InvoiceDto | null>(null);
  const [products, setProducts] = useState<Product[]>([]);
  const [form, setForm] = useState<FormState | null>(null);

  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const productById = useMemo(() => {
    const map = new Map<string, Product>();
    products.forEach(p => map.set(p.id, p));
    return map;
  }, [products]);

  const load = async () => {
    if (!invoiceId) return;
    try {
      setLoading(true);
      setError(null);

      const [inv, p] = await Promise.all([getInvoiceById(invoiceId), getProducts()]);
      setInvoice(inv);
      setProducts(p);

      setForm({
        dueDate: fmtYmd(inv.dueDate),
        currencyCode: inv.currencyCode,
        taxRatePercent: String(inv.taxRatePercent ?? 0),
        lines: (inv.lines ?? []).length
          ? (inv.lines ?? []).map(l => ({
              productId: l.productId,
              description: l.description,
              unitPrice: String(l.unitPrice),
              quantity: String(l.quantity),
            }))
          : [{ ...emptyLine }],
      });
    } catch (e: unknown) {
      const msg =
        e instanceof ApiError ? (e.problemDetails?.detail ?? e.message) :
        e instanceof Error ? e.message :
        "Failed to load invoice";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [invoiceId]);

  const handleHeaderChange = (e: ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setForm(prev => (prev ? { ...prev, [name]: value } : prev));
  };

  const handleLineChange = (idx: number, e: ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    setForm(prev =>
      prev ? { ...prev, lines: prev.lines.map((l, i) => (i === idx ? { ...l, [name]: value } : l)) } : prev
    );

    if (name === "productId") {
      const p = productById.get(value);
      if (p) {
        setForm(prev =>
          prev
            ? {
                ...prev,
                lines: prev.lines.map((l, i) =>
                  i === idx ? { ...l, description: l.description || p.name, unitPrice: l.unitPrice || String(p.unitPrice) } : l
                ),
              }
            : prev
        );
      }
    }
  };

  const addLine = () => setForm(prev => (prev ? { ...prev, lines: [...prev.lines, { ...emptyLine }] } : prev));
  const removeLine = (idx: number) =>
    setForm(prev =>
      prev ? { ...prev, lines: prev.lines.length <= 1 ? prev.lines : prev.lines.filter((_, i) => i !== idx) } : prev
    );

  const validateAndBuildRequest = (): UpdateInvoiceRequest => {
    if (!form) throw new Error("Form not ready");

    const taxRatePercent = Number(form.taxRatePercent);
    if (!Number.isFinite(taxRatePercent) || taxRatePercent < 0 || taxRatePercent > 100)
      throw new Error("TaxRatePercent must be between 0 and 100");

    const lines = form.lines.map((l, idx) => {
      const unitPrice = Number(l.unitPrice);
      const quantity = Number(l.quantity);

      if (!l.productId) throw new Error(`Line ${idx + 1}: Product is required`);
      if (!l.description?.trim()) throw new Error(`Line ${idx + 1}: Description is required`);
      if (!Number.isFinite(unitPrice) || unitPrice < 0) throw new Error(`Line ${idx + 1}: UnitPrice must be >= 0`);
      if (!Number.isFinite(quantity) || quantity <= 0) throw new Error(`Line ${idx + 1}: Quantity must be > 0`);

      return { productId: l.productId, description: l.description.trim(), unitPrice, quantity };
    });

    return {
      dueDate: form.dueDate,
      currencyCode: (form.currencyCode || "INR").trim().toUpperCase(),
      taxRatePercent,
      lines,
    };
  };

  const handleSave = async (e: FormEvent) => {
    e.preventDefault();
    if (!invoiceId) return;

    try {
      setSaving(true);
      setError(null);

      const req = validateAndBuildRequest();
      const updated = await updateInvoice(invoiceId, req);

      navigate(`/invoices/${updated.id}`);
    } catch (e: unknown) {
      const msg =
        e instanceof ApiError ? (e.problemDetails?.detail ?? e.message) :
        e instanceof Error ? e.message :
        "Failed to update invoice";
      setError(msg);
    } finally {
      setSaving(false);
    }
  };

  if (!invoiceId) {
    return (
      <div style={{ padding: 16 }}>
        <p>Missing invoice id.</p>
        <Link to="/invoices">Back</Link>
      </div>
    );
  }

  const canEdit = invoice?.status === "Draft";

  return (
    <div style={{ padding: 16, maxWidth: 1200, margin: "0 auto" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12 }}>
        <div>
          <h2 style={{ margin: 0 }}>Edit Invoice</h2>
          <div style={{ marginTop: 6 }}>
            <Link to={`/invoices/${invoiceId}`}>&larr; Back to details</Link>
          </div>
        </div>
        <button type="button" onClick={() => navigate(`/invoices/${invoiceId}`)}>Cancel</button>
      </div>

      {error && (
        <div style={{ marginTop: 12, padding: 10, border: "1px solid #f3b", background: "#fff5f8" }}>
          {error}
        </div>
      )}

      {loading || !form || !invoice ? (
        <p style={{ marginTop: 12 }}>{loading ? "Loading..." : "Not found."}</p>
      ) : !canEdit ? (
        <div style={{ marginTop: 12, padding: 12, border: "1px solid #ddd" }}>
          <p>This invoice is <strong>{invoice.status}</strong>. Only Draft invoices can be edited.</p>
          <Link to={`/invoices/${invoiceId}`}>Back</Link>
        </div>
      ) : (
        <form onSubmit={handleSave} style={{ marginTop: 12 }}>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
            <div>
              <label style={{ display: "block", marginBottom: 6 }}>Due Date</label>
              <input type="date" name="dueDate" value={form.dueDate} onChange={handleHeaderChange} style={{ width: "100%", padding: 8 }} />
            </div>

            <div>
              <label style={{ display: "block", marginBottom: 6 }}>Currency</label>
              <input name="currencyCode" value={form.currencyCode} onChange={handleHeaderChange} style={{ width: "100%", padding: 8 }} />
            </div>

            <div>
              <label style={{ display: "block", marginBottom: 6 }}>Tax rate (%)</label>
              <input name="taxRatePercent" value={form.taxRatePercent} onChange={handleHeaderChange} style={{ width: "100%", padding: 8 }} inputMode="decimal" />
            </div>
          </div>

          <h3 style={{ marginTop: 16 }}>Lines</h3>
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ddd", padding: 8 }}>Product</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ddd", padding: 8 }}>Description</th>
                <th style={{ textAlign: "right", borderBottom: "1px solid #ddd", padding: 8 }}>Unit Price</th>
                <th style={{ textAlign: "right", borderBottom: "1px solid #ddd", padding: 8 }}>Qty</th>
                <th style={{ borderBottom: "1px solid #ddd", padding: 8 }}></th>
              </tr>
            </thead>
            <tbody>
              {form.lines.map((l, idx) => (
                <tr key={idx}>
                  <td style={{ padding: 8, borderBottom: "1px solid #eee" }}>
                    <select name="productId" value={l.productId} onChange={e => handleLineChange(idx, e)} style={{ width: "100%", padding: 8 }}>
                      <option value="">Select product</option>
                      {products.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                    </select>
                  </td>

                  <td style={{ padding: 8, borderBottom: "1px solid #eee" }}>
                    <input name="description" value={l.description} onChange={e => handleLineChange(idx, e)} style={{ width: "100%", padding: 8 }} />
                  </td>

                  <td style={{ padding: 8, borderBottom: "1px solid #eee", textAlign: "right" }}>
                    <input name="unitPrice" value={l.unitPrice} onChange={e => handleLineChange(idx, e)} style={{ width: 120, padding: 8, textAlign: "right" }} inputMode="decimal" />
                  </td>

                  <td style={{ padding: 8, borderBottom: "1px solid #eee", textAlign: "right" }}>
                    <input name="quantity" value={l.quantity} onChange={e => handleLineChange(idx, e)} style={{ width: 90, padding: 8, textAlign: "right" }} inputMode="decimal" />
                  </td>

                  <td style={{ padding: 8, borderBottom: "1px solid #eee" }}>
                    <button type="button" onClick={() => removeLine(idx)} disabled={form.lines.length <= 1}>Remove</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <div style={{ marginTop: 10, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <button type="button" onClick={addLine}>+ Add line</button>
            <button type="submit" disabled={saving}>{saving ? "Saving..." : "Save"}</button>
          </div>
        </form>
      )}
    </div>
  );
}
