export type Customer = {
  id: string;
  name: string;
  businessName?: string | null;
  email?: string | null;
  phone?: string | null;
  billingAddress?: string | null;
  taxId?: string | null;
  isActive: boolean;
  createdAt: string;
};

export type CreateCustomerRequest = {
  name: string;
  businessName?: string;
  email?: string;
  phone?: string;
  billingAddress?: string;
  taxId?: string;
};
