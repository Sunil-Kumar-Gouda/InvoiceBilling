import { http, httpBlob } from "./http";
import type { CreateInvoiceRequest, InvoiceDto } from "../features/invoices/types";

export function getInvoices(): Promise<InvoiceDto[]> {
  return http<InvoiceDto[]>("/api/invoices");
}

export function createInvoice(request: CreateInvoiceRequest): Promise<InvoiceDto> {
  return http<InvoiceDto>("/api/invoices", { method: "POST", body: JSON.stringify(request) });
}

export function issueInvoice(id: string): Promise<{ message: string; invoiceId: string }> {
  return http<{ message: string; invoiceId: string }>(`/api/invoices/${id}/issue`, { method: "POST" });
}

function tryGetFileNameFromContentDisposition(value: string | null): string | null {
  if (!value) return null;

  // Examples:
  // content-disposition: attachment; filename="INV-20250101-120000.txt"
  // content-disposition: attachment; filename=INV-20250101-120000.txt
  const match = /filename\*?=(?:UTF-8''|")?([^;"\n]+)"?/i.exec(value);
  if (!match?.[1]) return null;

  // decodeURIComponent for filename* cases
  const raw = match[1].trim();
  try {
    return decodeURIComponent(raw);
  } catch {
    return raw;
  }
}

export async function downloadInvoiceFile(id: string): Promise<{ blob: Blob; fileName: string }> {
  const { blob, headers } = await httpBlob(`/api/invoices/${id}/pdf`, {
    method: "GET",
    headers: { Accept: "application/pdf" },
  });

  const cd = headers.get("content-disposition");
  const fileName = tryGetFileNameFromContentDisposition(cd) ?? `invoice-${id}.pdf`;

  return { blob, fileName };
}

