import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";

import type { Customer } from "../customers/types";
import type { InvoiceDto } from "./types";

import { getCustomers } from "../../api/customersApi";
import { downloadInvoiceFile, getInvoices, issueInvoice } from "../../api/invoicesApi";
import { ApiError } from "../../api/types";

const STATUSES = ["Draft", "Issued", "Paid", "Overdue"] as const;

function fmtDate(iso: string): string {
  return iso?.length >= 10 ? iso.substring(0, 10) : iso;
}

export default function InvoicesPage() {
  const navigate = useNavigate();

  const [customers, setCustomers] = useState<Customer[]>([]);
  const [invoices, setInvoices] = useState<InvoiceDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);

  // Filters + paging (minimum usable)
  const [status, setStatus] = useState<string>("");
  const [customerId, setCustomerId] = useState<string>("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  const [issuingId, setIssuingId] = useState<string | null>(null);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const customerNameById = useMemo(() => {
    const map = new Map<string, string>();
    customers.forEach(c => map.set(c.id, c.name));
    return map;
  }, [customers]);

  const load = async () => {
    try {
      setLoading(true);
      setError(null);
      setInfo(null);

      const [c, inv] = await Promise.all([
        getCustomers(),
        getInvoices({
          status: status || undefined,
          customerId: customerId || undefined,
          page,
          pageSize,
        }),
      ]);

      setCustomers(c);
      setInvoices(inv);
    } catch (e: unknown) {
      const msg =
        e instanceof ApiError ? (e.problemDetails?.detail ?? e.message) :
        e instanceof Error ? e.message :
        "Failed to load invoices";
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [status, customerId, page, pageSize]);

  const handleIssue = async (id: string) => {
    try {
      setIssuingId(id);
      setError(null);
      setInfo(null);

      const result = await issueInvoice(id);
      const baseMsg = result.wasNoOp ? "Invoice was already issued (no-op)." : "Invoice issued.";
      const queueMsg =
        result.jobEnqueued === false ? (result.jobEnqueueError ? ` PDF job enqueue failed: ${result.jobEnqueueError}` : " PDF job enqueue failed.") :
        "";
      setInfo(`${baseMsg}${queueMsg}`);

      await load();
    } catch (e: unknown) {
      const msg =
        e instanceof ApiError ? (e.problemDetails?.detail ?? e.message) :
        e instanceof Error ? e.message :
        "Failed to issue invoice";
      setError(msg);
    } finally {
      setIssuingId(null);
    }
  };

  const handleDownload = async (id: string) => {
    try {
      setDownloadingId(id);
      setError(null);
      setInfo(null);

      const { blob, fileName } = await downloadInvoiceFile(id);
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
      setDownloadingId(null);
    }
  };

  return (
    <div style={{ padding: 16, maxWidth: 1200, margin: "0 auto" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12 }}>
        <h2 style={{ margin: 0 }}>Invoices</h2>
        <button type="button" onClick={() => navigate("/invoices/new")}>
          + New Invoice
        </button>
      </div>

      <div style={{ marginTop: 12, display: "flex", gap: 12, alignItems: "end", flexWrap: "wrap" }}>
        <div>
          <label style={{ display: "block", marginBottom: 6 }}>Status</label>
          <select
            value={status}
            onChange={e => { setPage(1); setStatus(e.target.value); }}
            style={{ padding: 8, minWidth: 160 }}
          >
            <option value="">All</option>
            {STATUSES.map(s => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        </div>

        <div>
          <label style={{ display: "block", marginBottom: 6 }}>Customer</label>
          <select
            value={customerId}
            onChange={e => { setPage(1); setCustomerId(e.target.value); }}
            style={{ padding: 8, minWidth: 220 }}
          >
            <option value="">All</option>
            {customers.map(c => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </div>

        <div>
          <label style={{ display: "block", marginBottom: 6 }}>Page size</label>
          <select
            value={String(pageSize)}
            onChange={e => { setPage(1); setPageSize(Number(e.target.value)); }}
            style={{ padding: 8, minWidth: 120 }}
          >
            {[10, 25, 50, 100].map(n => (
              <option key={n} value={String(n)}>{n}</option>
            ))}
          </select>
        </div>

        <div style={{ display: "flex", gap: 8, marginLeft: "auto" }}>
          <button type="button" onClick={() => load()} disabled={loading}>Refresh</button>
          <button type="button" onClick={() => { setStatus(""); setCustomerId(""); setPage(1); }} disabled={loading}>Clear</button>
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

      <div style={{ marginTop: 12 }}>
        {loading ? (
          <p>Loading...</p>
        ) : invoices.length === 0 ? (
          <p>No invoices found.</p>
        ) : (
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ddd", padding: 8 }}>Invoice</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ddd", padding: 8 }}>Customer</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ddd", padding: 8 }}>Status</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ddd", padding: 8 }}>Issue</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ddd", padding: 8 }}>Due</th>
                <th style={{ textAlign: "right", borderBottom: "1px solid #ddd", padding: 8 }}>Total</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ddd", padding: 8 }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {invoices.map(inv => {
                const canIssue = inv.status === "Draft";
                const canDownload = !!inv.pdfS3Key;

                return (
                  <tr key={inv.id}>
                    <td style={{ padding: 8, borderBottom: "1px solid #eee" }}>
                      <Link to={`/invoices/${inv.id}`}>{inv.invoiceNumber}</Link>
                    </td>
                    <td style={{ padding: 8, borderBottom: "1px solid #eee" }}>
                      {customerNameById.get(inv.customerId) ?? inv.customerId}
                    </td>
                    <td style={{ padding: 8, borderBottom: "1px solid #eee" }}>
                      {inv.status}
                      {inv.status === "Issued" && !inv.pdfS3Key && (
                        <span
                          style={{
                            marginLeft: 8,
                            display: "inline-flex",
                            alignItems: "center",
                            padding: "2px 10px",
                            borderRadius: 999,
                            fontSize: 12,
                            border: "1px solid #fde68a",
                            background: "#fffbeb",
                            lineHeight: 1.6,
                          }}
                          title="PDF is being generated"
                        >
                          PDF pending
                        </span>
                      )}
                    </td>
                    <td style={{ padding: 8, borderBottom: "1px solid #eee" }}>{fmtDate(inv.issueDate)}</td>
                    <td style={{ padding: 8, borderBottom: "1px solid #eee" }}>{fmtDate(inv.dueDate)}</td>
                    <td style={{ padding: 8, borderBottom: "1px solid #eee", textAlign: "right" }}>
                      {inv.currencyCode} {inv.grandTotal.toFixed(2)}
                    </td>
                    <td style={{ padding: 8, borderBottom: "1px solid #eee" }}>
                      <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                        <button type="button" onClick={() => navigate(`/invoices/${inv.id}`)}>View</button>

                        <button
                          type="button"
                          onClick={() => handleIssue(inv.id)}
                          disabled={!canIssue || issuingId === inv.id}
                          title={!canIssue ? "Only Draft invoices can be issued" : ""}
                        >
                          {issuingId === inv.id ? "Issuing..." : "Issue"}
                        </button>

                        <button
                          type="button"
                          onClick={() => handleDownload(inv.id)}
                          disabled={!canDownload || downloadingId === inv.id}
                          title={!canDownload ? "PDF not ready yet" : ""}
                        >
                          {downloadingId === inv.id ? "Downloading..." : "Download"}
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}

        <div style={{ marginTop: 12, display: "flex", gap: 8, justifyContent: "flex-end" }}>
          <button type="button" onClick={() => setPage(p => Math.max(1, p - 1))} disabled={loading || page <= 1}>
            Prev
          </button>
          <span style={{ alignSelf: "center" }}>Page {page}</span>
          <button type="button" onClick={() => setPage(p => p + 1)} disabled={loading || invoices.length < pageSize}>
            Next
          </button>
        </div>
      </div>
    </div>
  );
}