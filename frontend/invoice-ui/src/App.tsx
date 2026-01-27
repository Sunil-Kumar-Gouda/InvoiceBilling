import { Link } from "react-router-dom";
import { useAuth } from "./auth/AuthContext";

function App() {
  const { isAuthenticated, logout } = useAuth();

  return (
    <div style={{ padding: "1rem" }}>
      <h1>InvoiceBilling</h1>

      <nav>
        <ul>
          {!isAuthenticated && (
            <li>
              <Link to="/login">Login</Link>
            </li>
          )}

          <li>
            <Link to="/customers">Customers</Link>
          </li>
          <li>
            <Link to="/products">Products</Link>
          </li>
          <li>
            <Link to="/invoices">Invoices</Link>
          </li>

          {isAuthenticated && (
            <li>
              <button
                type="button"
                onClick={logout}
                style={{ padding: 0, border: 0, background: "transparent", color: "#2563eb", cursor: "pointer" }}
              >
                Logout
              </button>
            </li>
          )}
        </ul>
      </nav>

      <p>
        Welcome to InvoiceBilling. {isAuthenticated ? "You are signed in." : "Please login to manage data."}
      </p>
    </div>
  );
}

export default App;
