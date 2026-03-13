import { Link, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

const NAV_LINKS = [
  { to: "/customers",    label: "Customers"     },
  { to: "/products",     label: "Products"      },
  { to: "/invoices",     label: "Invoices"      },
  { to: "/pdf-template", label: "PDF Template"  },
];

export default function NavBar() {
  const { isAuthenticated, logout } = useAuth();
  const { pathname } = useLocation();

  return (
    <nav style={{
      position: "sticky",
      top: 0,
      zIndex: 100,
      display: "flex",
      alignItems: "center",
      gap: 0,
      padding: "0 1.25rem",
      height: 52,
      background: "#1e293b",
      boxShadow: "0 1px 4px rgba(0,0,0,0.25)",
    }}>
      {/* Brand / Home */}
      <Link
        to="/"
        style={{
          fontWeight: 700,
          fontSize: 16,
          color: "#f8fafc",
          textDecoration: "none",
          marginRight: 24,
          letterSpacing: "-0.01em",
          whiteSpace: "nowrap",
        }}
      >
        📄 InvoiceBilling
      </Link>

      {/* Nav links */}
      <div style={{ display: "flex", alignItems: "center", gap: 4, flex: 1 }}>
        {NAV_LINKS.map(({ to, label }) => {
          const active = pathname === to || pathname.startsWith(to + "/");
          return (
            <Link
              key={to}
              to={to}
              style={{
                padding: "6px 12px",
                borderRadius: 6,
                fontSize: 14,
                fontWeight: active ? 600 : 400,
                color: active ? "#f8fafc" : "#94a3b8",
                textDecoration: "none",
                background: active ? "rgba(255,255,255,0.1)" : "transparent",
                transition: "background 0.15s, color 0.15s",
              }}
            >
              {label}
            </Link>
          );
        })}
      </div>

      {/* Auth actions */}
      <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
        {!isAuthenticated ? (
          <Link
            to="/login"
            style={{
              padding: "5px 14px",
              borderRadius: 6,
              fontSize: 14,
              color: "#f8fafc",
              textDecoration: "none",
              border: "1px solid rgba(255,255,255,0.25)",
            }}
          >
            Login
          </Link>
        ) : (
          <button
            type="button"
            onClick={logout}
            style={{
              padding: "5px 14px",
              borderRadius: 6,
              fontSize: 14,
              cursor: "pointer",
              background: "transparent",
              color: "#94a3b8",
              border: "1px solid rgba(255,255,255,0.15)",
            }}
          >
            Logout
          </button>
        )}
      </div>
    </nav>
  );
}
