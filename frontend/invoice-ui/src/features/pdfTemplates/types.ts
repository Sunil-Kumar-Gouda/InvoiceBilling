export type Align = "Left" | "Center" | "Right";

export type FontSpec = {
  family: string;
  size: number;
  bold: boolean;
  italic: boolean;
};

export type FieldKey =
  | "InvoiceNumber"
  | "InvoiceDate"
  | "DueDate"
  | "CompanyName"
  | "CompanyAddress"
  | "CompanyPhone"
  | "CustomerName"
  | "CustomerPhone"
  | "CustomerAddress"
  | "SubTotal"
  | "TaxTotal"
  | "GrandTotal"
  | "PaidTotal"
  | "BalanceDue"
  | "Status";

export type FieldPlacement = {
  id: string;          // UI id (not used by backend renderer unless you want)
  key: FieldKey;
  x: number;           // points
  y: number;           // points
  w: number;           // points
  h: number;           // points
  align: Align;
  font: FontSpec;
};

export type LinesTableColumnKey = "Description" | "Qty" | "Rate" | "Amount";

export type LinesTable = {
  x: number;
  y: number;
  w: number;
  h: number;
  headerFont: FontSpec;
  rowFont: FontSpec;
  columns: Array<{ key: LinesTableColumnKey; w: number; align: Align }>;
};

export type PdfTemplateDefinition = {
  version: number;
  page: { size: "A4"; width: number; height: number; margin: number };
  fields: FieldPlacement[];
  linesTable: LinesTable;
};

export const A4 = { width: 595, height: 842 }; // points

export const AVAILABLE_FIELDS: Array<{ key: FieldKey; label: string; defaultW: number; defaultH: number }> = [
  { key: "InvoiceNumber", label: "Invoice Number", defaultW: 160, defaultH: 18 },
  { key: "InvoiceDate", label: "Invoice Date", defaultW: 160, defaultH: 14 },
  { key: "DueDate", label: "Due Date", defaultW: 160, defaultH: 14 },
  { key: "CompanyName", label: "Company Name", defaultW: 280, defaultH: 18 },
  { key: "CompanyAddress", label: "Company Address", defaultW: 280, defaultH: 42 },
  { key: "CompanyPhone", label: "Company Phone", defaultW: 200, defaultH: 14 },
  { key: "CustomerName", label: "Customer Name", defaultW: 280, defaultH: 18 },
  { key: "CustomerPhone", label: "Customer Phone", defaultW: 200, defaultH: 14 },
  { key: "CustomerAddress", label: "Customer Address", defaultW: 280, defaultH: 42 },
  { key: "SubTotal", label: "Sub Total", defaultW: 140, defaultH: 14 },
  { key: "TaxTotal", label: "Tax Total", defaultW: 140, defaultH: 14 },
  { key: "GrandTotal", label: "Grand Total", defaultW: 160, defaultH: 18 },
  { key: "PaidTotal", label: "Paid Total", defaultW: 140, defaultH: 14 },
  { key: "BalanceDue", label: "Balance Due", defaultW: 140, defaultH: 14 },
  { key: "Status", label: "Status", defaultW: 120, defaultH: 14 },
];

export function defaultTemplate(): PdfTemplateDefinition {
  return {
    version: 1,
    page: { size: "A4", width: A4.width, height: A4.height, margin: 24 },
    fields: [
      {
        id: "f_invoiceNumber",
        key: "InvoiceNumber",
        x: 380,
        y: 48,
        w: 180,
        h: 18,
        align: "Right",
        font: { family: "Roboto", size: 14, bold: true, italic: false },
      },
      {
        id: "f_customerName",
        key: "CustomerName",
        x: 60,
        y: 140,
        w: 300,
        h: 18,
        align: "Left",
        font: { family: "Roboto", size: 12, bold: true, italic: false },
      },
    ],
    linesTable: {
      x: 60,
      y: 220,
      w: 475,
      h: 420,
      headerFont: { family: "Roboto", size: 10, bold: true, italic: false },
      rowFont: { family: "Roboto", size: 10, bold: false, italic: false },
      columns: [
        { key: "Description", w: 255, align: "Left" },
        { key: "Qty", w: 50, align: "Right" },
        { key: "Rate", w: 80, align: "Right" },
        { key: "Amount", w: 90, align: "Right" },
      ],
    },
  };
}

export function newId(prefix: string): string {
  // works in modern browsers; fallback for older
  return (globalThis.crypto?.randomUUID?.() ? `${prefix}_${crypto.randomUUID()}` : `${prefix}_${Date.now()}_${Math.random()}`);
}
