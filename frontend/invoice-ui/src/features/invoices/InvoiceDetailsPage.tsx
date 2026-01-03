import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";

import type { Customer } from "../customers/types";
import type { Product } from "../products/types";
import type { InvoiceDto } from "./types";

import { getCustomers } from "../../api/customersApi";
import { getProducts } from "../../api/productsApi";
import { downloadInvoiceFile, getInvoiceById, issueInvoice } from "../../api/invoicesApi";
import { ApiError } from "../../api/types";

function fmtDate(iso: string): string {
  return iso?.length >= 10 ? iso.substring(0, 10) : iso;
}

export default function InvoiceDetailsPage() {
  const { id } = useParams();
  const invoiceId = id ?? "";
  const navigate = useNavigate();

  const [invoice, setInvoice] = useState<InvoiceDto | null>(null);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [products, setProducts] = useState<Product[]>([]);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [issuing, setIssuing] = useState(false);
  const [downloading, setDownloading] = useState(false);

  const customerName = useMemo(() => {
    const c = customers.find(x => x.id === invoice?.customerId);
    return c?.name;
  }, [customers, invoice?.customerId]);

  const productNameById = useMemo(() => {
    const map = new Map<string, string>();
    products.forEach(p => map.set(p.id, p.name));
    return map;
  }, [products]);

  const load = async () => {
    if (!invoiceId) return;

    try {
      setLoading(true);
      setError(null);

      const [inv, c, p] = await Promise.all([getInvoiceById(invoiceId), getCustomers(), getProducts()]);
      setInvoice(inv);
      setCustomers(c);
      setProducts(p);
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

  const sleep = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

  const pollPdfReady = async () => {
    const timeoutMs = 20_000;
    const intervalMs = 2_000;
    const end = Date.now() + timeoutMs;

    while (Date.now() < end) {
      const inv = await getInvoiceById(invoiceId);
      setInvoice(inv);
      if (inv.pdfS3Key) return;
      await sleep(intervalMs);
    }
  };

  const handleIssue = async () => {
    if (!invoice) return;

    try {
      setIssuing(true);
      setError(null);
      await issueInvoice(invoice.id);

      await load();
      await pollPdfReady();
    } catch (e: unknown) {
      const msg =
        e instanceof ApiError ? (e.problemDetails?.detail ?? e.message) :
        e instanceof Error ? e.message :
        "Failed to issue invoice";
      setError(msg);
    } finally {
      setIssuing(false);
    }
  };

  const handleDownload = async () => {
    if (!invoice) return;

    try {
      setDownloading(true);
      setError(null);

      const { blob, fileName } = await downloadInvoiceFile(invoice.id);
      const url = URL.createObjectURL(blob);

      const a = document.createElement("a");
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } catch (e: unknown) {
      const msg =
        e instanceof ApiError ? (e.problemDetails?.detail ?? e.message) :
        e instanceof Error ? e.message :
        "Failed to download invoice";
      setError(msg);
    } finally {
      setDownloading(false);
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

  return (
    <div style={{ padding: 16, maxWidth: 1200, margin: "0 auto" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12 }}>
        <div>
          <h2 style={{ margin: 0 }}>Invoice Details</h2>
          <div style={{ marginTop: 6 }}>
            <Link to="/invoices">&larr; Back to list</Link>
          </div>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button type="button" onClick={() => navigate(`/invoices/${invoiceId}/edit`)} disabled={!invoice || invoice.status !== "Draft"}>
            Edit Draft
          </button>
          <button type="button" onClick={load} disabled={loading}>Refresh</button>
        </div>
      </div>

      {error && (
        <div style={{ marginTop: 12, padding: 10, border: "1px solid #f3b", background: "#fff5f8" }}>
          {error}
        </div>
      )}

      {loading || !invoice ? (
        <p style={{ marginTop: 12 }}>{loading ? "Loading..." : "Not found."}</p>
      ) : (
        <>
          <div style={{ marginTop: 12, padding: 12, border: "1px solid #ddd" }}>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
              <div><strong>Invoice:</strong> {invoice.invoiceNumber}</div>
              <div><strong>Status:</strong> {invoice.status}</div>
              <div><strong>Customer:</strong> {customerName ?? invoice.customerId}</div>

              <div><strong>Issue date:</strong> {fmtDate(invoice.issueDate)}</div>
              <div><strong>Due date:</strong> {fmtDate(invoice.dueDate)}</div>
              <div><strong>Currency:</strong> {invoice.currencyCode}</div>

              <div><strong>Subtotal:</strong> {invoice.subtotal.toFixed(2)}</div>
              <div><strong>Tax ({invoice.taxRatePercent.toFixed(2)}%):</strong> {invoice.taxTotal.toFixed(2)}</div>
              <div><strong>Total:</strong> {invoice.grandTotal.toFixed(2)}</div>
            </div>

            <div style={{ marginTop: 12, display: "flex", gap: 8, flexWrap: "wrap" }}>
              <button type="button" onClick={handleIssue} disabled={issuing || invoice.status !== "Draft"}>
                {issuing ? "Issuing..." : "Issue"}
              </button>

              <button type="button" onClick={handleDownload} disabled={downloading || !invoice.pdfS3Key}>
                {downloading ? "Downloading..." : "Download"}
              </button>

              {!invoice.pdfS3Key && <span style={{ alignSelf: "center" }}>PDF not ready yet.</span>}
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
                <th style={{ textAlign: "right", borderBottom: "1px solid #ddd", padding: 8 }}>Line Total</th>
              </tr>
            </thead>
            <tbody>
              {(invoice.lines ?? []).map(l => (
                <tr key={l.id}>
                  <td style={{ padding: 8, borderBottom: "1px solid #eee" }}>
                    {productNameById.get(l.productId) ?? l.productId}
                  </td>
                  <td style={{ padding: 8, borderBottom: "1px solid #eee" }}>{l.description}</td>
                  <td style={{ padding: 8, borderBottom: "1px solid #eee", textAlign: "right" }}>{l.unitPrice.toFixed(2)}</td>
                  <td style={{ padding: 8, borderBottom: "1px solid #eee", textAlign: "right" }}>{l.quantity}</td>
                  <td style={{ padding: 8, borderBottom: "1px solid #eee", textAlign: "right" }}>{l.lineTotal.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}
