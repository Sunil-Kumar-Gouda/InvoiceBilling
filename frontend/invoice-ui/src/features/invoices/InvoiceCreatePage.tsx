import { useEffect, useMemo, useState } from "react";
import type { ChangeEvent, FormEvent } from "react";
import { useNavigate } from "react-router-dom";

import type { Customer } from "../customers/types";
import type { Product } from "../products/types";
import type { CreateInvoiceRequest } from "./types";

import { getCustomers } from "../../api/customersApi";
import { getProducts } from "../../api/productsApi";
import { createInvoice } from "../../api/invoicesApi";
import { formatError, type ErrorInfo } from "../../api/errorFormat";
import ErrorBanner from "../../components/ErrorBanner";

type LineFormState = {
  productId: string;
  description: string;
  unitPrice: string;
  quantity: string;
};

type InvoiceFormState = {
  customerId: string;
  issueDate: string;
  dueDate: string;
  currencyCode: string;
  lines: LineFormState[];
};

const emptyLine: LineFormState = { productId: "", description: "", unitPrice: "", quantity: "1" };

function yyyyMmDd(d: Date): string {
  const yyyy = d.getFullYear();
  const mm = String(d.getMonth() + 1).padStart(2, "0");
  const dd = String(d.getDate()).padStart(2, "0");
  return `${yyyy}-${mm}-${dd}`;
}

function addDays(d: Date, days: number): Date {
  const copy = new Date(d);
  copy.setDate(copy.getDate() + days);
  return copy;
}

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

export default function InvoiceCreatePage() {
  const navigate = useNavigate();

  const [customers, setCustomers] = useState<Customer[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<ErrorInfo | null>(null);

  const [form, setForm] = useState<InvoiceFormState>(() => createEmptyForm());

  const productById = useMemo(() => {
    const map = new Map<string, Product>();
    products.forEach(p => map.set(p.id, p));
    return map;
  }, [products]);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        setError(null);
        const [c, p] = await Promise.all([getCustomers(), getProducts()]);
        setCustomers(c);
        setProducts(p);
      } catch (e: unknown) {
        setError(formatError(e));
      } finally {
        setLoading(false);
      }
    };

    load();
  }, []);

  const handleHeaderChange = (e: ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    setForm(prev => ({ ...prev, [name]: value }));
  };

  const handleLineChange = (idx: number, e: ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    setForm(prev => ({
      ...prev,
      lines: prev.lines.map((l, i) => (i === idx ? { ...l, [name]: value } : l)),
    }));

    if (name === "productId") {
      const p = productById.get(value);
      if (p) {
        setForm(prev => ({
          ...prev,
          lines: prev.lines.map((l, i) =>
            i === idx
              ? { ...l, description: l.description || p.name, unitPrice: l.unitPrice || String(p.unitPrice) }
              : l
          ),
          currencyCode: (prev.currencyCode || p.currencyCode || "INR").toUpperCase(),
        }));
      }
    }
  };

  const addLine = () => setForm(prev => ({ ...prev, lines: [...prev.lines, { ...emptyLine }] }));

  const removeLine = (idx: number) =>
    setForm(prev => ({
      ...prev,
      lines: prev.lines.length <= 1 ? prev.lines : prev.lines.filter((_, i) => i !== idx),
    }));

  const validateAndBuildRequest = (): CreateInvoiceRequest | null => {
    if (!form.customerId) {
      setError({ message: "Customer is required", kind: "validation" });
      return null;
    }
    if (!form.issueDate || !form.dueDate) {
      setError({ message: "Issue date and Due date are required", kind: "validation" });
      return null;
    }

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
      customerId: form.customerId,
      issueDate: form.issueDate,
      dueDate: form.dueDate,
      currencyCode: (form.currencyCode || "INR").trim().toUpperCase(),
      lines,
    };
  };

  const handleCreate = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);

    let req: CreateInvoiceRequest;
    try {
      const built = validateAndBuildRequest();
      if (!built) return;
      req = built;
    } catch (err: unknown) {
      setError({ message: err instanceof Error ? err.message : "Invalid form", kind: "validation" });
      return;
    }

    try {
      setSaving(true);
      const created = await createInvoice(req);
      navigate(`/invoices/${created.id}`);
    } catch (e: unknown) {
      setError(formatError(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div style={{ padding: 16, maxWidth: 1200, margin: "0 auto" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <h2 style={{ margin: 0 }}>New Invoice</h2>
        <button type="button" onClick={() => navigate("/invoices")}>Back</button>
      </div>

      {error && <ErrorBanner error={error} onDismiss={() => setError(null)} />}

      {loading ? (
        <p style={{ marginTop: 12 }}>Loading...</p>
      ) : (
        <form onSubmit={handleCreate} style={{ marginTop: 12 }}>
          <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr 1fr 1fr", gap: 12 }}>
            <div>
              <label style={{ display: "block", marginBottom: 6 }}>
                Customer <span style={{ color: "crimson" }}>*</span>
              </label>
              <select name="customerId" value={form.customerId} onChange={handleHeaderChange} style={{ width: "100%", padding: 8 }}>
                <option value="">Select customer</option>
                {customers.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>

            <div>
              <label style={{ display: "block", marginBottom: 6 }}>
                Issue Date <span style={{ color: "crimson" }}>*</span>
              </label>
              <input type="date" name="issueDate" value={form.issueDate} onChange={handleHeaderChange} style={{ width: "100%", padding: 8 }} />
            </div>

            <div>
              <label style={{ display: "block", marginBottom: 6 }}>
                Due Date <span style={{ color: "crimson" }}>*</span>
              </label>
              <input type="date" name="dueDate" value={form.dueDate} onChange={handleHeaderChange} style={{ width: "100%", padding: 8 }} />
            </div>

            <div>
              <label style={{ display: "block", marginBottom: 6 }}>Currency</label>
              <input name="currencyCode" value={form.currencyCode} onChange={handleHeaderChange} style={{ width: "100%", padding: 8 }} />
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
            <button type="submit" disabled={saving}>{saving ? "Saving..." : "Create Invoice"}</button>
          </div>
        </form>
      )}
    </div>
  );
}
