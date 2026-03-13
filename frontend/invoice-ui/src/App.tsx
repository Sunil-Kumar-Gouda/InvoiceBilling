import { Link } from "react-router-dom";
import { useAuth } from "./auth/AuthContext";

function App() {
  const { isAuthenticated } = useAuth();

  return (
    <div>
      <h1 style={{ marginTop: 0, fontSize: 24, color: "#1e293b" }}>Welcome to InvoiceBilling</h1>
      <p style={{ color: "#475569" }}>
        {isAuthenticated
          ? "You are signed in. Use the navigation above to manage your data."
          : "Please login to manage your invoices, customers and products."}
      </p>

      {!isAuthenticated && (
        <Link
          to="/login"
          style={{
            display: "inline-block",
            marginTop: 8,
            padding: "8px 20px",
            borderRadius: 6,
            background: "#2563eb",
            color: "#fff",
            textDecoration: "none",
            fontWeight: 600,
            fontSize: 14,
          }}
        >
          Login →
        </Link>
      )}

      {isAuthenticated && (
        <div style={{ marginTop: 16, display: "flex", gap: 12, flexWrap: "wrap" }}>
          {[
            { to: "/customers",    label: "Customers"    },
            { to: "/products",     label: "Products"     },
            { to: "/invoices",     label: "Invoices"     },
            { to: "/pdf-template", label: "PDF Template" },
          ].map(({ to, label }) => (
            <Link
              key={to}
              to={to}
              style={{
                padding: "10px 20px",
                borderRadius: 8,
                border: "1px solid #e2e8f0",
                background: "#fff",
                color: "#1e293b",
                textDecoration: "none",
                fontWeight: 500,
                fontSize: 14,
                boxShadow: "0 1px 2px rgba(0,0,0,0.06)",
              }}
            >
              {label}
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}

export default App;
