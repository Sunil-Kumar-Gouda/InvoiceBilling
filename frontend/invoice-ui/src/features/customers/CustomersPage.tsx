import { useEffect, useState } from "react";
import type { ChangeEvent, FormEvent } from "react";
import { createCustomer, getCustomers } from "../../api/customersApi";
import { formatError, type ErrorInfo } from "../../api/errorFormat";
import ErrorBanner from "../../components/ErrorBanner";
import type { Customer, CreateCustomerRequest } from "./types";

function CustomersPage() {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<ErrorInfo | null>(null);

  const [form, setForm] = useState<CreateCustomerRequest>({
    name: "",
    email: "",
    businessName: "",
    phone: "",
    billingAddress: "",
    taxId: ""
  });

  useEffect(() => {
    console.log("CustomerPage mounted -> loading customers");
    const fetchCustomers = async () => {
      try {
        setLoading(true);
        setError(null);

        const data = await getCustomers();
        setCustomers(data);
      } catch (err: any) {
        const fe = formatError(err);
        setError(fe);
      } finally {
        setLoading(false);
      }
    };

    fetchCustomers();
  }, []);

  const handleInputChange = (
    e:  ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
  ) => {
    const { name, value } = e.target;
    setForm(prev => ({ ...prev, [name]: value }));
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();

    if (!form.name.trim()) {
      alert("Name is required");
      return;
    }

    try {
      setError(null);

      const created: Customer = await createCustomer(form);
      setCustomers(prev => [...prev, created]);

      setForm({
        name: "",
        email: "",
        businessName: "",
        phone: "",
        billingAddress: "",
        taxId: ""
      });
    } catch (err: any) {
      const fe = formatError(err);
      setError(fe);
    }
  };

  return (
    <div style={{ padding: "1rem" }}>
      <h1>Customers</h1>

      {loading && <p>Loading customers...</p>}
      {error && <ErrorBanner error={error} onDismiss={() => setError(null)} />}

      <section style={{ marginBottom: "2rem" }}>
        <h2>Create Customer</h2>
        <form onSubmit={handleSubmit} style={{ maxWidth: 400, display: "grid", gap: "0.5rem" }}>
          <div>
            <label>
              Name* <br />
              <input
                name="name"
                value={form.name}
                onChange={handleInputChange}
                required
              />
            </label>
          </div>

          <div>
            <label>
              Business Name <br />
              <input
                name="businessName"
                value={form.businessName ?? ""}
                onChange={handleInputChange}
              />
            </label>
          </div>

          <div>
            <label>
              Email <br />
              <input
                name="email"
                type="email"
                value={form.email ?? ""}
                onChange={handleInputChange}
              />
            </label>
          </div>

          <div>
            <label>
              Phone <br />
              <input
                name="phone"
                value={form.phone ?? ""}
                onChange={handleInputChange}
              />
            </label>
          </div>

          <div>
            <label>
              Billing Address <br />
              <textarea
                name="billingAddress"
                value={form.billingAddress ?? ""}
                onChange={handleInputChange}
              />
            </label>
          </div>

          <div>
            <label>
              Tax Id (e.g. GSTIN) <br />
              <input
                name="taxId"
                value={form.taxId ?? ""}
                onChange={handleInputChange}
              />
            </label>
          </div>

          <button type="submit">Save Customer</button>
        </form>
      </section>

      <section>
        <h2>Customer List</h2>
        {customers.length === 0 ? (
          <p>No customers found.</p>
        ) : (
          <table border={1} cellPadding={4} style={{ borderCollapse: "collapse" }}>
            <thead>
              <tr>
                <th>Name</th>
                <th>Business</th>
                <th>Email</th>
                <th>Phone</th>
                <th>Tax Id</th>
                <th>Created At</th>
              </tr>
            </thead>
            <tbody>
              {customers.map(c => (
                <tr key={c.id}>
                  <td>{c.name}</td>
                  <td>{c.businessName}</td>
                  <td>{c.email}</td>
                  <td>{c.phone}</td>
                  <td>{c.taxId}</td>
                  <td>{new Date(c.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}

export default CustomersPage;
