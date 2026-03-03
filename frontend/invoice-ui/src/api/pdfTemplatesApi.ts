import { http, httpBlob } from "./http";
import { ApiError } from "./types";
import type { PdfTemplateDefinition } from "../features/pdfTemplates/types";

export async function getActivePdfTemplate(): Promise<PdfTemplateDefinition | null> {
  try {
    return await http<PdfTemplateDefinition>("/api/pdf-templates/active");
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) return null;
    throw e;
  }
}

export async function saveActivePdfTemplate(def: PdfTemplateDefinition): Promise<void> {
  await http<void>("/api/pdf-templates/active", {
    method: "PUT",
    body: JSON.stringify(def),
  });
}

export async function previewPdfTemplate(def: PdfTemplateDefinition, invoiceId: string): Promise<Blob> {
  const { blob } = await httpBlob(`/api/pdf-templates/preview/${encodeURIComponent(invoiceId)}`, {
    method: "POST",
    body: JSON.stringify(def),
    headers: { "Content-Type": "application/json" },
  });
  return blob;
}
