import { http } from "./http";
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
