export type InvoiceLine = {
  productId: string;
  description: string;
  unitPrice: number;
  quantity: number;
};

export type CreateInvoiceRequest = {
  customerId: string;
  issueDate: string;
  dueDate: string;
  currencyCode: string;
  lines: InvoiceLine[];
};

export type InvoiceDto = {
  id: string;
  invoiceNumber: string;
  customerId: string;
  status: string;
  issueDate: string;
  dueDate: string;
  currencyCode: string;
  subtotal: number;
  taxTotal: number;
  grandTotal: number;
  pdfS3Key?: string | null;
  createdAt: string;
};
