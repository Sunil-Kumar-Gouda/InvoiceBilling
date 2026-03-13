import type { ReactNode } from "react";
import NavBar from "../components/NavBar";

export default function Layout({ children }: { children: ReactNode }) {
  return (
    <div style={{ minHeight: "100vh", display: "flex", flexDirection: "column", background: "#f8fafc" }}>
      <NavBar />
      <main style={{ flex: 1, padding: "1.25rem 1.5rem", maxWidth: 1280, width: "100%", margin: "0 auto", boxSizing: "border-box" }}>
        {children}
      </main>
    </div>
  );
}
