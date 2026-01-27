import React from "react";
import type { ErrorInfo } from "../api/errorFormat";

type Props = {
  error: ErrorInfo;
  onDismiss?: () => void;
};

function computeTitle(error: ErrorInfo): string {
  if (error.title && error.title.trim().length > 0) return error.title;
  if (error.kind === "validation") return "Validation error";
  if (error.kind === "conflict") return "Conflict";
  return "Error";
}

function computeStyle(kind: ErrorInfo["kind"]): React.CSSProperties {
  switch (kind) {
    case "validation":
      return { border: "1px solid #e0b4b4", background: "#fff6f6" };
    case "conflict":
      return { border: "1px solid #f3c78a", background: "#fff7e6" };
    default:
      return { border: "1px solid #f5c2c7", background: "#f8d7da" };
  }
}

export default function ErrorBanner({ error, onDismiss }: Props) {
  const title = computeTitle(error);
  const style = computeStyle(error.kind);

  return (
    <div style={{ ...style, padding: 12, marginBottom: 12 }}>
      <div style={{ display: "flex", justifyContent: "space-between", gap: 12 }}>
        <div>
          <strong>{title}</strong>
          {typeof error.status === "number" && (
            <span style={{ opacity: 0.75 }}> (HTTP {error.status})</span>
          )}
        </div>

        {onDismiss && (
          <button
            type="button"
            onClick={onDismiss}
            style={{ border: 0, background: "transparent", cursor: "pointer", opacity: 0.8 }}
            aria-label="Dismiss error"
            title="Dismiss"
          >
            ×
          </button>
        )}
      </div>

      <div style={{ marginTop: 8, whiteSpace: "pre-line" }}>{error.message}</div>

      {error.lines && error.lines.length > 0 && (
        <ul style={{ marginTop: 8, marginBottom: 0, paddingLeft: 18 }}>
          {error.lines.map((l, i) => (
            <li key={i}>{l}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
