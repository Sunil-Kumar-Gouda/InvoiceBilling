import { useEffect, useMemo, useState, type CSSProperties } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";

import type { Customer } from "../customers/types";
import type { Product } from "../products/types";
import type { InvoiceDto, InvoicePdfStatus, InvoiceStatusDto } from "./types";

import { getCustomers } from "../../api/customersApi";
import { getProducts } from "../../api/productsApi";
import { downloadInvoiceFile, getInvoiceById, getInvoiceStatus, issueInvoice } from "../../api/invoicesApi";
import { ApiError } from "../../api/types";

function fmtDate(iso: string): string {
  return iso?.length >= 10 ? iso.substring(0, 10) : iso;
}

function statusPillStyle(status: string): CSSProperties {
  const base: CSSProperties = {
    display: "inline-flex",
    alignItems: "center",
    padding: "2px 10px",
    borderRadius: 999,
    fontSize: 12,
    border: "1px solid #ddd",
    background: "#fafafa",
    lineHeight: 1.6,
  };

  if (status === "Draft") return { ...base, borderColor: "#c7d2fe", background: "#eef2ff" };
  if (status === "Issued") return { ...base, borderColor: "#bbf7d0", background: "#f0fdf4" };
  if (status === "Paid") return { ...base, borderColor: "#a7f3d0", background: "#ecfdf5" };
  if (status === "Overdue") return { ...base, borderColor: "#fecaca", background: "#fef2f2" };
  return base;
}

function pdfPillStyle(pdfStatus: InvoicePdfStatus): CSSProperties {
  const base: CSSProperties = {
    display: "inline-flex",
    alignItems: "center",
    padding: "2px 10px",
    borderRadius: 999,
    fontSize: 12,
    border: "1px solid #ddd",
    background: "#fafafa",
    lineHeight: 1.6,
  };

  if (pdfStatus === "Ready") return { ...base, borderColor: "#bbf7d0", background: "#f0fdf4" };
  if (pdfStatus === "Pending") return { ...base, borderColor: "#fde68a", background: "#fffbeb" };
  return { ...base, borderColor: "#e5e7eb", background: "#fafafa" };
}

export default function InvoiceDetailsPage() {
  const { id } = useParams();
  const invoiceId = id ?? "";
  const navigate = useNavigate();

  const [invoice, setInvoice] = useState<InvoiceDto | null>(null);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [products, setProducts] = useState<Product[]>([]);

  const [statusSnapshot, setStatusSnapshot] = useState<InvoiceStatusDto | null>(null);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);

  const [issuing, setIssuing] = useState(false);
  const [pollingPdf, setPollingPdf] = useState(false);
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

  const pdfStatus: InvoicePdfStatus = useMemo(() => {
    if (statusSnapshot?.pdfStatus) return statusSnapshot.pdfStatus;

    // Fallback (older backend or transient status endpoint issues)
    if (!invoice) return "NotIssued";
    if (invoice.status !== "Issued") return "NotIssued";
    return invoice.pdfS3Key ? "Ready" : "Pending";
  }, [invoice, statusSnapshot?.pdfStatus]);

  const canDownload = pdfStatus === "Ready";

  const load = async () => {
    if (!invoiceId) return;

    try {
      setLoading(true);
      setError(null);

      const [inv, c, p] = await Promise.all([getInvoiceById(invoiceId), getCustomers(), getProducts()]);
      setInvoice(inv);
      setCustomers(c);
      setProducts(p);

      try {
        const st = await getInvoiceStatus(invoiceId);
        setStatusSnapshot(st);
      } catch {
        // Status endpoint is UX-only; do not fail the page load
        setStatusSnapshot(null);
      }
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
    if (!invoiceId) return;

    const timeoutMs = 30_000;
    const intervalMs = 2_000;
    const end = Date.now() + timeoutMs;

    setPollingPdf(true);
    try {
      while (Date.now() < end) {
        const st = await getInvoiceStatus(invoiceId);
        setStatusSnapshot(st);

        if (st.pdfStatus === "Ready") return;

        await sleep(intervalMs);
      }
    } finally {
      setPollingPdf(false);
    }
  };

  const handleIssue = async () => {
    if (!invoice) return;

    try {
      setIssuing(true);
      setError(null);
      setInfo(null);

      const result = await issueInvoice(invoice.id);

      const baseMsg = result.wasNoOp ? "Invoice was already issued (no-op)." : "Invoice issued.";
      const queueMsg =
        result.jobEnqueued === false ? (result.jobEnqueueError ? ` PDF job enqueue failed: ${result.jobEnqueueError}` : " PDF job enqueue failed.") :
        "";
      setInfo(`${baseMsg}${queueMsg}`);

      await load();

      // After issuing, poll lightweight status endpoint until PDF is ready (or timeout)
      if ((invoice.status === "Draft" || result.wasNoOp) && pdfStatus !== "Ready") {
        await pollPdfReady();
      }
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
      setInfo(null);

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

      {info && (
        <div style={{ marginTop: 12, padding: 10, border: "1px solid #bde", background: "#f2f8ff" }}>
          {info}
        </div>
      )}

      {loading || !invoice ? (
        <p style={{ marginTop: 12 }}>{loading ? "Loading..." : "Not found."}</p>
      ) : (
        <>
          <div style={{ marginTop: 12, padding: 12, border: "1px solid #ddd" }}>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
              <div><strong>Invoice:</strong> {invoice.invoiceNumber}</div>
              <div>
                <strong>Status:</strong>{" "}
                <span style={statusPillStyle(invoice.status)}>{invoice.status}</span>
              </div>
              <div><strong>Customer:</strong> {customerName ?? invoice.customerId}</div>

              <div><strong>Issue date:</strong> {fmtDate(invoice.issueDate)}</div>
              <div><strong>Due date:</strong> {fmtDate(invoice.dueDate)}</div>
              <div><strong>Currency:</strong> {invoice.currencyCode}</div>

              <div><strong>Subtotal:</strong> {invoice.subtotal.toFixed(2)}</div>
              <div><strong>Tax ({invoice.taxRatePercent.toFixed(2)}%):</strong> {invoice.taxTotal.toFixed(2)}</div>
              <div><strong>Total:</strong> {invoice.grandTotal.toFixed(2)}</div>

              <div style={{ gridColumn: "1 / -1" }}>
                <strong>PDF:</strong>{" "}
                <span style={pdfPillStyle(pdfStatus)}>{pdfStatus}</span>
                {pollingPdf && <span style={{ marginLeft: 8, fontSize: 12 }}>Checking...</span>}
              </div>
            </div>

            <div style={{ marginTop: 12, display: "flex", gap: 8, flexWrap: "wrap" }}>
              <button type="button" onClick={handleIssue} disabled={issuing || invoice.status !== "Draft"}>
                {issuing ? "Issuing..." : "Issue"}
              </button>

              <button type="button" onClick={handleDownload} disabled={downloading || !canDownload}>
                {downloading ? "Downloading..." : "Download"}
              </button>

              {!canDownload && (
                <span style={{ alignSelf: "center" }}>
                  {invoice.status !== "Issued" ? "Issue the invoice to generate a PDF." : "PDF is being generated."}
                </span>
              )}
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
