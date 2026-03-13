import { useNavigate } from "react-router-dom";

interface BackButtonProps {
  /** Route to fall back to if there's no browser history (e.g. direct link). */
  fallback?: string;
  label?: string;
}

export default function BackButton({ fallback = "/", label = "← Back" }: BackButtonProps) {
  const navigate = useNavigate();

  const handleClick = () => {
    if (window.history.length > 1) {
      navigate(-1);
    } else {
      navigate(fallback);
    }
  };

  return (
    <button
      type="button"
      onClick={handleClick}
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 4,
        padding: "5px 12px",
        borderRadius: 6,
        fontSize: 13,
        cursor: "pointer",
        background: "transparent",
        color: "#475569",
        border: "1px solid #cbd5e1",
        fontWeight: 500,
      }}
    >
      {label}
    </button>
  );
}
