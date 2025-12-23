import { http } from "./http";
import type { Customer, CreateCustomerRequest } from "../features/customers/types";

export function getCustomers(): Promise<Customer[]> {
  return http<Customer[]>("/api/customers");
}

export function getCustomerById(id: string): Promise<Customer> {
  return http<Customer>(`/api/customers/${id}`);
}

export function createCustomer(request: CreateCustomerRequest): Promise<Customer> {
  return http<Customer>("/api/customers", {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export function updateCustomer(id: string, request: CreateCustomerRequest): Promise<void> {
  return http<void>(`/api/customers/${id}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });
}

export function deleteCustomer(id: string): Promise<void> {
  return http<void>(`/api/customers/${id}`, { method: "DELETE" });
}
