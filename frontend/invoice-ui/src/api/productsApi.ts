import { http } from "./http";
import type { Product, CreateProductRequest } from "../features/products/types";

export function getProducts(): Promise<Product[]> {
  return http<Product[]>("/api/products");
}

export function createProduct(request: CreateProductRequest): Promise<Product> {
  return http<Product>("/api/products", { method: "POST", body: JSON.stringify(request) });
}

export function updateProduct(id: string, request: CreateProductRequest): Promise<void> {
  return http<void>(`/api/products/${id}`, { method: "PUT", body: JSON.stringify(request) });
}

export function deleteProduct(id: string): Promise<void> {
  return http<void>(`/api/products/${id}`, { method: "DELETE" });
}
