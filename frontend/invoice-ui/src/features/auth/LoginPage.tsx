import React, { useEffect, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { login, register } from "../../api/authApi";
import { formatError, type ErrorInfo } from "../../api/errorFormat";
import { useAuth } from "../../auth/AuthContext";
import ErrorBanner from "../../components/ErrorBanner";

type LocationState = {
  from?: { pathname?: string };
};

export default function LoginPage() {
  const nav = useNavigate();
  const location = useLocation();
  const { setToken, isAuthenticated } = useAuth();

  const [mode, setMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ErrorInfo | null>(null);

  const state = location.state as LocationState | null;
  const redirectTo = state?.from?.pathname && state.from.pathname !== "/login" ? state.from.pathname : "/";

  useEffect(() => {
    if (isAuthenticated) {
      // Already logged in; redirect away from login page.
      nav(redirectTo, { replace: true });
    }
  }, [isAuthenticated, nav, redirectTo]);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);

    try {
      const resp = mode === "login"
        ? await login(email, password)
        : await register(email, password, displayName || undefined);

      setToken(resp.accessToken);
      nav(redirectTo, { replace: true });
    } catch (err) {
      setError(formatError(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div style={{ maxWidth: 420, margin: "2rem auto", padding: "1rem" }}>
      <h2 style={{ marginTop: 0 }}>InvoiceBilling</h2>
      <p style={{ marginTop: 0, opacity: 0.8 }}>
        {mode === "login" ? "Sign in to continue." : "Create an account."}
      </p>

      {error && <ErrorBanner error={error} onDismiss={() => setError(null)} />}

      <form onSubmit={onSubmit}>
        <div style={{ marginBottom: "0.75rem" }}>
          <label style={{ display: "block", marginBottom: 4 }}>Email</label>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            style={{ width: "100%", padding: "0.5rem" }}
          />
        </div>

        <div style={{ marginBottom: "0.75rem" }}>
          <label style={{ display: "block", marginBottom: 4 }}>Password</label>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            style={{ width: "100%", padding: "0.5rem" }}
          />
        </div>

        {mode === "register" && (
          <div style={{ marginBottom: "0.75rem" }}>
            <label style={{ display: "block", marginBottom: 4 }}>Display name (optional)</label>
            <input
              type="text"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              style={{ width: "100%", padding: "0.5rem" }}
            />
          </div>
        )}

        <button
          type="submit"
          disabled={busy}
          style={{ width: "100%", padding: "0.6rem", cursor: busy ? "not-allowed" : "pointer" }}
        >
          {busy ? "Please wait..." : mode === "login" ? "Login" : "Register"}
        </button>
      </form>

      <div style={{ marginTop: "1rem", display: "flex", justifyContent: "space-between" }}>
        <button
          type="button"
          onClick={() => setMode(mode === "login" ? "register" : "login")}
          style={{ padding: 0, border: 0, background: "transparent", color: "#2563eb", cursor: "pointer" }}
        >
          {mode === "login" ? "Need an account? Register" : "Have an account? Login"}
        </button>

        <Link to="/" style={{ color: "#2563eb" }}>
          Home
        </Link>
      </div>
    </div>
  );
}
