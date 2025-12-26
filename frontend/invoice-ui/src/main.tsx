import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, Routes, Route } from "react-router-dom";
import App from "./App";
import CustomersPage from "./features/customers/CustomersPage";
import ProductsPage from "./features/products/ProductsPage";
import InvoicesPage from "./features/invoices/InvoicesPage";
import "./index.css";

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<App />} />
        <Route path="/customers" element={<CustomersPage />} />
        <Route path="/products" element={<ProductsPage />} />
        <Route path="/invoices" element={<InvoicesPage />} />
      </Routes>
    </BrowserRouter>
  </React.StrictMode>
);
