import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, Routes, Route } from "react-router-dom";
import App from "./App";
import { AuthProvider } from "./auth/AuthContext";
import RequireAuth from "./auth/RequireAuth";
import CustomersPage from "./features/customers/CustomersPage";
import ProductsPage from "./features/products/ProductsPage";
import InvoicesPage from "./features/invoices/InvoicesPage";
import InvoiceCreatePage from "./features/invoices/InvoiceCreatePage";
import InvoiceDetailsPage from "./features/invoices/InvoiceDetailsPage";
import InvoiceEditPage from "./features/invoices/InvoiceEditPage";
import LoginPage from "./features/auth/LoginPage";
import "./index.css";

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<App />} />
          <Route path="/login" element={<LoginPage />} />

          <Route
            path="/customers"
            element={
              <RequireAuth>
                <CustomersPage />
              </RequireAuth>
            }
          />
          <Route
            path="/products"
            element={
              <RequireAuth>
                <ProductsPage />
              </RequireAuth>
            }
          />

          <Route
            path="/invoices"
            element={
              <RequireAuth>
                <InvoicesPage />
              </RequireAuth>
            }
          />
          <Route
            path="/invoices/new"
            element={
              <RequireAuth>
                <InvoiceCreatePage />
              </RequireAuth>
            }
          />
          <Route
            path="/invoices/:id"
            element={
              <RequireAuth>
                <InvoiceDetailsPage />
              </RequireAuth>
            }
          />
          <Route
            path="/invoices/:id/edit"
            element={
              <RequireAuth>
                <InvoiceEditPage />
              </RequireAuth>
            }
          />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  </React.StrictMode>
);
