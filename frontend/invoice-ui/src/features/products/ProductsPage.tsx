import { useEffect, useMemo, useState } from "react";
import type { ChangeEvent, FormEvent } from "react";

import type { Product, CreateProductRequest } from "./types";
import { getProducts, createProduct, updateProduct, deleteProduct } from "../../api/productsApi";
import { formatError, type ErrorInfo } from "../../api/errorFormat";
import ErrorBanner from "../../components/ErrorBanner";

type ProductFormState = {
  name: string;
  sku: string;
  unitPrice: string;     // keep as string in the form, convert on submit
  currencyCode: string;
};

const emptyForm: ProductFormState = {
  name: "",
  sku: "",
  unitPrice: "",
  currencyCode: "INR",
};

function safeFormatMoney(amount: number, currencyCode: string): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: "currency",
      currency: currencyCode,
    }).format(amount);
  } catch {
    return `${currencyCode} ${amount.toFixed(2)}`;
  }
}

export default function ProductsPage() {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<ErrorInfo | null>(null);

  const [form, setForm] = useState<ProductFormState>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);

  const isEditing = editingId !== null;

  const submitButtonText = useMemo(() => (isEditing ? "Update Product" : "Create Product"), [isEditing]);

  const loadProducts = async () => {
    try {
      setLoading(true);
      setError(null);

      const data = await getProducts();
      setProducts(data);
    } catch (e: unknown) {
      setError(formatError(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    console.log("ProductsPage mounted -> loading products");
    void loadProducts();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleInputChange = (e: ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;

    setForm(prev => ({
      ...prev,
      [name]: value,
    }));
  };

  const startEdit = (p: Product) => {
    setEditingId(p.id);
    setForm({
      name: p.name ?? "",
      sku: p.sku ?? "",
      unitPrice: String(p.unitPrice ?? ""),
      currencyCode: p.currencyCode ?? "INR",
    });
    setError(null);
  };

  const cancelEdit = () => {
    setEditingId(null);
    setForm(emptyForm);
    setError(null);
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();

    const name = form.name.trim();
    if (!name) {
      setError({ message: "Name is required.", kind: "validation" });
      return;
    }

    const unitPriceNumber = Number(form.unitPrice);
    if (!Number.isFinite(unitPriceNumber) || unitPriceNumber < 0) {
      setError({ message: "Unit Price must be a valid number (0 or greater).", kind: "validation" });
      return;
    }

    const req: CreateProductRequest = {
      name,
      sku: form.sku.trim() ? form.sku.trim() : undefined,
      unitPrice: unitPriceNumber,
      currencyCode: form.currencyCode.trim() ? form.currencyCode.trim().toUpperCase() : "INR",
    };

    try {
      setError(null);

      if (!editingId) {
        const created = await createProduct(req);

        // Keep list stable; you can also re-fetch by calling loadProducts()
        setProducts(prev => [created, ...prev]);
      } else {
        await updateProduct(editingId, req);

        // Update local list (no re-fetch)
        setProducts(prev =>
          prev.map(p =>
            p.id === editingId
              ? {
                  ...p,
                  name: req.name,
                  sku: req.sku ?? null,
                  unitPrice: req.unitPrice,
                  currencyCode: req.currencyCode ?? p.currencyCode,
                }
              : p
          )
        );
      }

      cancelEdit();
    } catch (e: unknown) {
      setError(formatError(e));
    }
  };

  const handleDelete = async (id: string) => {
    const ok = window.confirm("Delete this product? (This will soft-delete it in the API)");
    if (!ok) return;

    try {
      setError(null);
      await deleteProduct(id);
      setProducts(prev => prev.filter(p => p.id !== id));
      if (editingId === id) cancelEdit();
    } catch (e: unknown) {
      setError(formatError(e));
    }
  };

  return (
    <div style={{ padding: 16, maxWidth: 1100, margin: "0 auto" }}>
      <h2 style={{ marginBottom: 8 }}>Products</h2>

      <div style={{ display: "flex", gap: 8, alignItems: "center", marginBottom: 12 }}>
        <button type="button" onClick={loadProducts} disabled={loading}>
          Refresh
        </button>
        {loading && <span>Loading...</span>}
      </div>

      {error && <ErrorBanner error={error} onDismiss={() => setError(null)} />}

      <form onSubmit={handleSubmit} style={{ marginBottom: 16, padding: 12, border: "1px solid #ddd" }}>
        <h3 style={{ marginTop: 0 }}>{isEditing ? "Edit Product" : "Create Product"}</h3>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
          <div>
            <label style={{ display: "block", marginBottom: 6 }}>
              Name <span style={{ color: "crimson" }}>*</span>
            </label>
            <input
              name="name"
              value={form.name}
              onChange={handleInputChange}
              placeholder="e.g., Consulting Service"
              style={{ width: "100%", padding: 8 }}
            />
          </div>

          <div>
            <label style={{ display: "block", marginBottom: 6 }}>SKU</label>
            <input
              name="sku"
              value={form.sku}
              onChange={handleInputChange}
              placeholder="e.g., SKU-001"
              style={{ width: "100%", padding: 8 }}
            />
          </div>

          <div>
            <label style={{ display: "block", marginBottom: 6 }}>
              Unit Price <span style={{ color: "crimson" }}>*</span>
            </label>
            <input
              name="unitPrice"
              value={form.unitPrice}
              onChange={handleInputChange}
              placeholder="e.g., 999.00"
              inputMode="decimal"
              style={{ width: "100%", padding: 8 }}
            />
          </div>

          <div>
            <label style={{ display: "block", marginBottom: 6 }}>Currency</label>
            <input
              name="currencyCode"
              value={form.currencyCode}
              onChange={handleInputChange}
              placeholder="INR"
              style={{ width: "100%", padding: 8 }}
            />
          </div>
        </div>

        <div style={{ marginTop: 12, display: "flex", gap: 8 }}>
          <button type="submit">{submitButtonText}</button>
          {isEditing && (
            <button type="button" onClick={cancelEdit}>
              Cancel
            </button>
          )}
        </div>
      </form>

      {products.length === 0 ? (
        <p>No products found.</p>
      ) : (
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>Name</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>SKU</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>Unit Price</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>Currency</th>
                <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>Created</th>
                <th style={{ borderBottom: "1px solid #ccc", padding: 8 }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {products.map(p => (
                <tr key={p.id}>
                  <td style={{ borderBottom: "1px solid #eee", padding: 8 }}>{p.name}</td>
                  <td style={{ borderBottom: "1px solid #eee", padding: 8 }}>{p.sku ?? ""}</td>
                  <td style={{ borderBottom: "1px solid #eee", padding: 8 }}>
                    {safeFormatMoney(p.unitPrice, p.currencyCode)}
                  </td>
                  <td style={{ borderBottom: "1px solid #eee", padding: 8 }}>{p.currencyCode}</td>
                  <td style={{ borderBottom: "1px solid #eee", padding: 8 }}>
                    {p.createdAt ? new Date(p.createdAt).toLocaleString() : ""}
                  </td>
                  <td style={{ borderBottom: "1px solid #eee", padding: 8, whiteSpace: "nowrap" }}>
                    <button type="button" onClick={() => startEdit(p)} style={{ marginRight: 8 }}>
                      Edit
                    </button>
                    <button type="button" onClick={() => handleDelete(p.id)}>
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
