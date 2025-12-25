export type Product = {
  id: string;
  name: string;
  sku?: string | null;
  unitPrice: number;
  currencyCode: string;
  isActive: boolean;
  createdAt: string;
};

export type CreateProductRequest = {
  name: string;
  sku?: string;
  unitPrice: number;
  currencyCode?: string;
};
