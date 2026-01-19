export type InvoiceLineInput = {
  productId: string;
  description: string;
  unitPrice: number;
  quantity: number;
};

export type CreateInvoiceRequest = {
  customerId: string;
  issueDate: string; // yyyy-MM-dd
  dueDate: string; // yyyy-MM-dd
  currencyCode: string; // 3-letter ISO (e.g., INR)
  lines: InvoiceLineInput[];
};

export type UpdateInvoiceRequest = {
  dueDate: string; // yyyy-MM-dd
  currencyCode: string; // 3-letter ISO (e.g., INR)
  taxRatePercent: number; // 0..100
  lines: InvoiceLineInput[];
};

export type InvoiceLineDto = {
  id: string;
  productId: string;
  description: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
};

export type InvoiceDto = {
  id: string;
  invoiceNumber: string;
  customerId: string;
  status: string;
  issueDate: string;
  dueDate: string;

  currencyCode: string;
  taxRatePercent: number;

  subtotal: number;
  taxTotal: number;
  grandTotal: number;

  // Day 13: Payments (backend may omit these on older versions)
  paidTotal?: number;
  balanceDue?: number;

  pdfS3Key?: string | null;
  createdAt: string;

  // Present for GET /api/invoices/{id}; list endpoint may omit it
  lines?: InvoiceLineDto[];
};

export type GetInvoicesQuery = {
  status?: string;
  customerId?: string;
  issueDateFrom?: string; // yyyy-MM-dd
  issueDateTo?: string; // yyyy-MM-dd
  page?: number;
  pageSize?: number;
};

export type InvoicePdfStatus = "NotIssued" | "Pending" | "Ready";

export type InvoiceStatusDto = {
  id: string;
  status: string;
  pdfStatus: InvoicePdfStatus;
  pdfDownloadUrl?: string | null;

  // Day 13: Payments (backend may omit these on older versions)
  paidTotal?: number;
  balanceDue?: number;
};

export type IssueInvoiceResult = {
  // Backward compatible with earlier API contract (message + invoiceId)
  message?: string;
  invoiceId?: string;

  // Newer contract fields (CQRS/idempotency improvements)
  wasNoOp?: boolean;
  jobEnqueued?: boolean;
  jobEnqueueError?: string | null;

  // Some versions may also return the updated invoice snapshot
  invoice?: InvoiceDto;
};

// Day 13: Payments
export type PaymentDto = {
  id: string;
  invoiceId: string;
  amount: number;
  paidAt: string; // ISO date-time
  method?: string | null;
  reference?: string | null;
  note?: string | null;
  createdAt: string; // ISO date-time
};

export type RecordPaymentRequest = {
  amount: number;
  paidAtUtc: string; // ISO date-time (UTC)
  method?: string | null;
  reference?: string | null;
  note?: string | null;
};

export type RecordPaymentResponse = {
  message?: string;
  invoice: InvoiceDto;
  payment: PaymentDto;
};
