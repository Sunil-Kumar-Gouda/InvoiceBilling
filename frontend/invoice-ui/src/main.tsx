import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, Routes, Route } from "react-router-dom";
import App from "./App";
import { AuthProvider } from "./auth/AuthContext";
import RequireAuth from "./auth/RequireAuth";
import Layout from "./layouts/Layout";
import CustomersPage from "./features/customers/CustomersPage";
import ProductsPage from "./features/products/ProductsPage";
import InvoicesPage from "./features/invoices/InvoicesPage";
import InvoiceCreatePage from "./features/invoices/InvoiceCreatePage";
import InvoiceDetailsPage from "./features/invoices/InvoiceDetailsPage";
import InvoiceEditPage from "./features/invoices/InvoiceEditPage";
import LoginPage from "./features/auth/LoginPage";
import PdfTemplateDesignerPage from "./features/pdfTemplates/PdfTemplateDesignerPage";
import "./index.css";

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          {/* Login is outside Layout so it can be centered standalone */}
          <Route path="/login" element={<LoginPage />} />

          {/* All other routes share the persistent NavBar via Layout */}
          <Route path="/" element={<Layout><App /></Layout>} />

          <Route path="/customers" element={<Layout><RequireAuth><CustomersPage /></RequireAuth></Layout>} />
          <Route path="/products"  element={<Layout><RequireAuth><ProductsPage /></RequireAuth></Layout>} />

          <Route path="/invoices"          element={<Layout><RequireAuth><InvoicesPage /></RequireAuth></Layout>} />
          <Route path="/invoices/new"      element={<Layout><RequireAuth><InvoiceCreatePage /></RequireAuth></Layout>} />
          <Route path="/invoices/:id"      element={<Layout><RequireAuth><InvoiceDetailsPage /></RequireAuth></Layout>} />
          <Route path="/invoices/:id/edit" element={<Layout><RequireAuth><InvoiceEditPage /></RequireAuth></Layout>} />

          <Route path="/pdf-template" element={<Layout><RequireAuth><PdfTemplateDesignerPage /></RequireAuth></Layout>} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  </React.StrictMode>
);
