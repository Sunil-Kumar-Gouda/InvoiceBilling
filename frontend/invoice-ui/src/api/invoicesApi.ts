import { http, httpBlob } from "./http";
import type { CreateInvoiceRequest, InvoiceDto, UpdateInvoiceRequest, GetInvoicesQuery, InvoiceStatusDto, IssueInvoiceResult } from "../features/invoices/types";

function buildQuery(q?: GetInvoicesQuery): string {
  if (!q) return "";
  const p = new URLSearchParams();

  if (q.status) p.set("status", q.status);
  if (q.customerId) p.set("customerId", q.customerId);
  if (q.issueDateFrom) p.set("issueDateFrom", q.issueDateFrom);
  if (q.issueDateTo) p.set("issueDateTo", q.issueDateTo);
  if (typeof q.page === "number") p.set("page", String(q.page));
  if (typeof q.pageSize === "number") p.set("pageSize", String(q.pageSize));

  const s = p.toString();
  return s ? `?${s}` : "";
}

export function getInvoices(query?: GetInvoicesQuery): Promise<InvoiceDto[]> {
  return http<InvoiceDto[]>(`/api/invoices${buildQuery(query)}`);
}

export function getInvoiceById(id: string): Promise<InvoiceDto> {
  return http<InvoiceDto>(`/api/invoices/${id}`);
}

export function getInvoiceStatus(id: string): Promise<InvoiceStatusDto> {
  return http<InvoiceStatusDto>(`/api/invoices/${id}/status`);
}

export function createInvoice(request: CreateInvoiceRequest): Promise<InvoiceDto> {
  return http<InvoiceDto>("/api/invoices", { method: "POST", body: JSON.stringify(request) });
}

export function updateInvoice(id: string, request: UpdateInvoiceRequest): Promise<InvoiceDto> {
  return http<InvoiceDto>(`/api/invoices/${id}`, { method: "PUT", body: JSON.stringify(request) });
}

export function issueInvoice(id: string): Promise<IssueInvoiceResult> {
  return http<IssueInvoiceResult>(`/api/invoices/${id}/issue`, { method: "POST" });
}

function tryGetFileNameFromContentDisposition(value: string | null): string | null {
  if (!value) return null;

  // Handles:
  //  - filename="INV-123.pdf"
  //  - filename=INV-123.pdf
  //  - filename*=UTF-8''INV-123.pdf
  const parts = value.split(";").map(x => x.trim());

  for (const part of parts) {
    const lower = part.toLowerCase();

    if (lower.startsWith("filename*=")) {
      const raw = part.substring("filename*=".length);
      const match = raw.match(/utf-8''(.+)$/i);
      if (match?.[1]) return decodeURIComponent(match[1].replace(/^"|"$/g, ""));
    }

    if (lower.startsWith("filename=")) {
      return part.substring("filename=".length).replace(/^"|"$/g, "");
    }
  }

  return null;
}

export async function downloadInvoiceFile(id: string): Promise<{ blob: Blob; fileName: string }> {
  const { blob, headers } = await httpBlob(`/api/invoices/${id}/pdf`, {
    method: "GET",
    headers: { Accept: "application/pdf" },
  });

  // Requires backend CORS: Access-Control-Expose-Headers: Content-Disposition
  const cd = headers.get("content-disposition");
  const fileName = tryGetFileNameFromContentDisposition(cd) ?? `invoice-${id}.pdf`;

  return { blob, fileName };
}
