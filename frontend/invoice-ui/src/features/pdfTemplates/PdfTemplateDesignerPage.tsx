import React, { useEffect, useMemo, useRef, useState } from "react";
import ErrorBanner from "../../components/ErrorBanner";
import { formatError, type ErrorInfo } from "../../api/errorFormat";
import { getActivePdfTemplate, previewPdfTemplate, saveActivePdfTemplate } from "../../api/pdfTemplatesApi";
import {
  AVAILABLE_FIELDS,
  defaultTemplate,
  newId,
  type Align,
  type FieldKey,
  type FieldPlacement,
  type LineElement,
  type LineOrientation,
  type PdfTemplateDefinition,
} from "./types";

// ─── Drag types ──────────────────────────────────────────────────────────────

type DragMode = "move" | "resize";
type ElementKind = "field" | "line";

type DragState = {
  kind: ElementKind;
  mode: DragMode;
  elementId: string;
  startX: number;
  startY: number;
  origX: number;
  origY: number;
  origW: number;   // field only
  origH: number;   // field only
  origLen: number; // line only
};

// ─── Font / colour constants ─────────────────────────────────────────────────

const FONT_FAMILIES: Array<{ value: string; label: string }> = [
  { value: "Roboto",          label: "Roboto"          },
  { value: "Helvetica",       label: "Helvetica"       },
  { value: "Arial",           label: "Arial"           },
  { value: "Times New Roman", label: "Times New Roman" },
  { value: "Courier New",     label: "Courier New"     },
  { value: "Georgia",         label: "Georgia"         },
  { value: "Verdana",         label: "Verdana"         },
  { value: "Trebuchet MS",    label: "Trebuchet MS"    },
];

const FONT_SIZES = [7, 8, 9, 10, 11, 12, 13, 14, 16, 18, 20, 24, 28, 32, 36, 48];

const LINE_THICKNESSES = [0.5, 1, 1.5, 2, 3, 4, 6, 8];

const COLOR_PRESETS: Array<{ hex: string; label: string }> = [
  { hex: "#000000", label: "Black"       },
  { hex: "#1a1a1a", label: "Near Black"  },
  { hex: "#374151", label: "Dark Gray"   },
  { hex: "#6b7280", label: "Gray"        },
  { hex: "#9ca3af", label: "Light Gray"  },
  { hex: "#ffffff", label: "White"       },
  { hex: "#1e40af", label: "Dark Blue"   },
  { hex: "#2563eb", label: "Blue"        },
  { hex: "#60a5fa", label: "Light Blue"  },
  { hex: "#065f46", label: "Dark Green"  },
  { hex: "#16a34a", label: "Green"       },
  { hex: "#86efac", label: "Light Green" },
  { hex: "#7f1d1d", label: "Dark Red"    },
  { hex: "#dc2626", label: "Red"         },
  { hex: "#f87171", label: "Light Red"   },
  { hex: "#78350f", label: "Dark Orange" },
  { hex: "#ea580c", label: "Orange"      },
  { hex: "#7c3aed", label: "Purple"      },
];

const ALIGNMENTS: Align[] = ["Left", "Center", "Right"];
const DEFAULT_COLOR = "#000000";

// ─── Helpers ─────────────────────────────────────────────────────────────────

function clamp(n: number, min: number, max: number) {
  return Math.max(min, Math.min(max, n));
}

function downloadOrOpenPdf(blob: Blob) {
  const url = URL.createObjectURL(blob);
  window.open(url, "_blank", "noopener,noreferrer");
}

// ─── Small shared components ─────────────────────────────────────────────────

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <div style={{
      fontWeight: 600, fontSize: 11, color: "#6b7280",
      textTransform: "uppercase", letterSpacing: "0.05em",
      marginBottom: 6, marginTop: 14,
    }}>
      {children}
    </div>
  );
}

function PropRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label style={{ display: "block", marginBottom: 2 }}>
      <span style={{ display: "block", fontSize: 11, fontWeight: 600, marginBottom: 3, color: "#374151" }}>
        {label}
      </span>
      {children}
    </label>
  );
}

function ColorPicker({ value, onChange }: { value: string; onChange: (hex: string) => void }) {
  return (
    <div>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(9, 1fr)", gap: 4, marginBottom: 8 }}>
        {COLOR_PRESETS.map(p => (
          <button
            key={p.hex}
            type="button"
            title={p.label}
            onClick={() => onChange(p.hex)}
            style={{
              width: "100%", aspectRatio: "1", background: p.hex, padding: 0,
              border: value.toLowerCase() === p.hex.toLowerCase()
                ? "2px solid #2563eb" : "1px solid rgba(0,0,0,0.18)",
              borderRadius: 3, cursor: "pointer",
            }}
          />
        ))}
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
        <input
          type="color" value={value} onChange={e => onChange(e.target.value)}
          style={{ width: 36, height: 30, padding: 2, border: "1px solid #d1d5db", borderRadius: 4, cursor: "pointer" }}
          title="Custom colour"
        />
        <input
          type="text" value={value} maxLength={7} placeholder="#000000"
          onChange={e => { if (/^#[0-9a-fA-F]{0,6}$/.test(e.target.value)) onChange(e.target.value); }}
          style={{ flex: 1, padding: "4px 6px", fontFamily: "monospace", fontSize: 12, border: "1px solid #d1d5db", borderRadius: 4 }}
        />
        <div style={{ width: 24, height: 24, background: value, border: "1px solid rgba(0,0,0,0.2)", borderRadius: 4, flexShrink: 0 }} />
      </div>
    </div>
  );
}

// ─── Main page ────────────────────────────────────────────────────────────────

export default function PdfTemplateDesignerPage() {
  const [loading, setLoading]       = useState(true);
  const [template, setTemplate]     = useState<PdfTemplateDefinition>(() => defaultTemplate());
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [scale, setScale]           = useState(1.0);
  const [error, setError]           = useState<ErrorInfo | null>(null);
  const [previewInvoiceId, setPreviewInvoiceId] = useState("");
  const [rawJson, setRawJson]                   = useState<string>("");

  const dragRef   = useRef<DragState | null>(null);
  const canvasRef = useRef<HTMLDivElement | null>(null);

  const pageW = template.page.width  * scale;
  const pageH = template.page.height * scale;

  // Sentinel id used when the LinesTable block itself is selected
  const LINES_TABLE_ID = "__linesTable__";

  // Resolve which element is selected
  const selectedField = useMemo(
    () => template.fields.find(f => f.id === selectedId) ?? null,
    [template.fields, selectedId],
  );
  const selectedLine = useMemo(
    () => (template.lines ?? []).find(l => l.id === selectedId) ?? null,
    [template.lines, selectedId],
  );
  const linesTableSelected = selectedId === LINES_TABLE_ID;

  // ── Load ───────────────────────────────────────────────────────────────────
  useEffect(() => {
    (async () => {
      try {
        setLoading(true);
        const existing = await getActivePdfTemplate();
        const raw = existing ?? defaultTemplate();
        const normalized: PdfTemplateDefinition = {
          ...raw,
          lines: raw.lines ?? [],   // back-compat: old templates have no lines array
          fields: raw.fields.map((f, i) => ({
            align: "Left" as const,
            color: DEFAULT_COLOR,
            ...f,
            id: f.id && f.id.trim() !== "" ? f.id : newId(`f${i}`),
          })),
        };
        setTemplate(normalized);
      } catch (e) {
        setError(formatError(e));
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  useEffect(() => {
    setRawJson(JSON.stringify(template, null, 2));
  }, [template]);

  // ── Field CRUD ─────────────────────────────────────────────────────────────
  function updateField(fieldId: string, patch: Partial<FieldPlacement>) {
    if (!fieldId) return;
    setTemplate(prev => ({
      ...prev,
      fields: prev.fields.map(f => (f.id === fieldId ? { ...f, ...patch } : f)),
    }));
  }

  function removeField(fieldId: string) {
    if (!fieldId) return;
    setTemplate(prev => ({ ...prev, fields: prev.fields.filter(f => f.id !== fieldId) }));
    if (selectedId === fieldId) setSelectedId(null);
  }

  function addField(key: FieldKey) {
    const meta   = AVAILABLE_FIELDS.find(x => x.key === key)!;
    const margin = template.page.margin;
    const nextY  = clamp(
      margin + 20 + template.fields.length * 18,
      margin,
      template.page.height - margin - meta.defaultH,
    );
    const f: FieldPlacement = {
      id: newId("f"), key,
      x: margin, y: nextY,
      w: meta.defaultW, h: meta.defaultH,
      align: "Left", color: DEFAULT_COLOR,
      font: { family: "Roboto", size: 11, bold: false, italic: false },
    };
    setTemplate(prev => ({ ...prev, fields: [...prev.fields, f] }));
    setSelectedId(f.id);
  }

  // ── Line CRUD ──────────────────────────────────────────────────────────────
  function updateLine(lineId: string, patch: Partial<LineElement>) {
    if (!lineId) return;
    setTemplate(prev => ({
      ...prev,
      lines: (prev.lines ?? []).map(l => (l.id === lineId ? { ...l, ...patch } : l)),
    }));
  }

  function removeLine(lineId: string) {
    if (!lineId) return;
    setTemplate(prev => ({ ...prev, lines: (prev.lines ?? []).filter(l => l.id !== lineId) }));
    if (selectedId === lineId) setSelectedId(null);
  }

  function addLine(orientation: LineOrientation) {
    const margin = template.page.margin;
    const pageW  = template.page.width;
    const pageH  = template.page.height;
    const centerX = Math.round(pageW / 2);
    const centerY = Math.round(pageH / 2);

    const line: LineElement = {
      id: newId("line"),
      orientation,
      x: orientation === "H" ? margin          : centerX,
      y: orientation === "H" ? centerY         : margin,
      length: orientation === "H"
        ? pageW - margin * 2   // span full content width
        : pageH - margin * 2,  // span full content height
      thickness: 1,
      color: DEFAULT_COLOR,
    };
    setTemplate(prev => ({ ...prev, lines: [...(prev.lines ?? []), line] }));
    setSelectedId(line.id);
  }

  // ── LinesTable CRUD ────────────────────────────────────────────────────────
  function updateLinesTable(patch: Partial<PdfTemplateDefinition["linesTable"]>) {
    setTemplate(prev => ({
      ...prev,
      linesTable: { ...prev.linesTable, ...patch },
    }));
  }

  function updateColumn(colIndex: number, patch: { header?: string; w?: number; align?: string }) {
    setTemplate(prev => {
      const cols = prev.linesTable.columns.map((c, i) =>
        i === colIndex ? { ...c, ...patch } : c,
      );
      return { ...prev, linesTable: { ...prev.linesTable, columns: cols } };
    });
  }

  function updateLinesTableFont(
    which: "headerFont" | "rowFont",
    patch: Partial<PdfTemplateDefinition["linesTable"]["headerFont"]>,
  ) {
    setTemplate(prev => ({
      ...prev,
      linesTable: {
        ...prev.linesTable,
        [which]: { ...prev.linesTable[which], ...patch },
      },
    }));
  }

  // ── Pointer / drag ─────────────────────────────────────────────────────────
  function getLocalPoint(e: PointerEvent | React.PointerEvent) {
    const canvas = canvasRef.current;
    if (!canvas) return { x: 0, y: 0 };
    const r = canvas.getBoundingClientRect();
    return { x: (e.clientX - r.left) / scale, y: (e.clientY - r.top) / scale };
  }

  function onFieldPointerDown(e: React.PointerEvent, fieldId: string, mode: DragMode) {
    e.preventDefault(); e.stopPropagation();
    if (!fieldId || fieldId.startsWith("__missing_")) return;
    const f = template.fields.find(x => x.id === fieldId);
    if (!f) return;
    setSelectedId(fieldId);
    const pt = getLocalPoint(e);
    dragRef.current = {
      kind: "field", mode, elementId: fieldId,
      startX: pt.x, startY: pt.y,
      origX: f.x, origY: f.y, origW: f.w, origH: f.h, origLen: 0,
    };
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
  }

  function onLinePointerDown(e: React.PointerEvent, lineId: string, mode: DragMode) {
    e.preventDefault(); e.stopPropagation();
    const l = (template.lines ?? []).find(x => x.id === lineId);
    if (!l) return;
    setSelectedId(lineId);
    const pt = getLocalPoint(e);
    dragRef.current = {
      kind: "line", mode, elementId: lineId,
      startX: pt.x, startY: pt.y,
      origX: l.x, origY: l.y, origW: 0, origH: 0, origLen: l.length,
    };
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
  }

  function onCanvasPointerMove(e: React.PointerEvent) {
    const drag = dragRef.current;
    if (!drag) return;
    const pt     = getLocalPoint(e);
    const dx     = pt.x - drag.startX;
    const dy     = pt.y - drag.startY;
    const margin = template.page.margin;

    if (drag.kind === "field") {
      if (drag.mode === "move") {
        updateField(drag.elementId, {
          x: clamp(drag.origX + dx, margin, template.page.width  - margin - drag.origW),
          y: clamp(drag.origY + dy, margin, template.page.height - margin - drag.origH),
        });
      } else {
        updateField(drag.elementId, {
          w: clamp(drag.origW + dx, 40, template.page.width  - margin - drag.origX),
          h: clamp(drag.origH + dy, 12, template.page.height - margin - drag.origY),
        });
      }
    } else {
      // line
      const line = (template.lines ?? []).find(l => l.id === drag.elementId);
      if (!line) return;
      if (drag.mode === "move") {
        if (line.orientation === "H") {
          updateLine(drag.elementId, {
            x: clamp(drag.origX + dx, margin, template.page.width - margin - line.length),
            y: clamp(drag.origY + dy, margin, template.page.height - margin),
          });
        } else {
          updateLine(drag.elementId, {
            x: clamp(drag.origX + dx, margin, template.page.width - margin),
            y: clamp(drag.origY + dy, margin, template.page.height - margin - line.length),
          });
        }
      } else {
        // resize = change length
        const delta = line.orientation === "H" ? dx : dy;
        updateLine(drag.elementId, {
          length: clamp(drag.origLen + delta, 20,
            line.orientation === "H"
              ? template.page.width  - margin - line.x
              : template.page.height - margin - line.y),
        });
      }
    }
  }

  function onCanvasPointerUp() { dragRef.current = null; }

  // ── Save / preview ─────────────────────────────────────────────────────────
  async function onSave() {
    try {
      setError(null);
      await saveActivePdfTemplate(template);
      alert("Saved template.");
    } catch (e) { setError(formatError(e)); }
  }

  async function onPreview() {
    try {
      setError(null);
      if (!previewInvoiceId.trim()) {
        setError({ kind: "validation", title: "Preview needs an invoice",
          message: "Enter an existing InvoiceId (GUID) to preview PDF rendering.",
          status: 400, lines: [] });
        return;
      }
      downloadOrOpenPdf(await previewPdfTemplate(template, previewInvoiceId.trim()));
    } catch (e) { setError(formatError(e)); }
  }

  function onApplyRawJson() {
    try {
      setError(null);
      const parsed = JSON.parse(rawJson) as PdfTemplateDefinition;
      if (!parsed?.page?.width || !parsed?.page?.height || !Array.isArray(parsed.fields))
        throw new Error("Invalid template JSON structure.");
      parsed.lines = parsed.lines ?? [];
      setTemplate(parsed);
      alert("Applied JSON.");
    } catch (e: unknown) {
      setError({ kind: "validation", title: "Invalid JSON",
        message: e instanceof Error ? e.message : "Invalid JSON", status: 400, lines: [] });
    }
  }

  if (loading) return <div style={{ padding: 16 }}>Loading...</div>;

  // Shared styles
  const inputStyle: React.CSSProperties = {
    width: "100%", padding: "5px 6px",
    border: "1px solid #d1d5db", borderRadius: 4, fontSize: 13,
    boxSizing: "border-box",
  };

  const removeBtn: React.CSSProperties = {
    padding: "8px 12px", width: "100%", cursor: "pointer",
    background: "#fee2e2", border: "1px solid #fca5a5",
    color: "#b91c1c", borderRadius: 4, fontWeight: 600, marginTop: 16,
  };

  return (
    <div style={{ padding: 16, display: "grid", gridTemplateColumns: "260px 1fr 320px", gap: 16 }}>

      {/* ── Left: palette ────────────────────────────────────────────────── */}
      <div>
        <h2 style={{ marginTop: 0 }}>PDF Designer</h2>
        {error && <ErrorBanner error={error} onDismiss={() => setError(null)} />}

        {/* Scale */}
        <div style={{ marginBottom: 12 }}>
          <label style={{ display: "block", fontWeight: 600 }}>Scale</label>
          <input type="range" min={0.6} max={1.6} step={0.1} value={scale}
            onChange={e => setScale(Number(e.target.value))} />
          <div style={{ opacity: 0.75 }}>{scale.toFixed(1)}×</div>
        </div>

        {/* Field palette */}
        <div style={{ border: "1px solid #e5e7eb", padding: 12, marginBottom: 12 }}>
          <div style={{ fontWeight: 600, marginBottom: 8 }}>Fields</div>
          <div style={{ display: "grid", gap: 6 }}>
            {AVAILABLE_FIELDS.map(f => (
              <button key={f.key} type="button" onClick={() => addField(f.key)}
                style={{ textAlign: "left", padding: "8px 10px", cursor: "pointer" }}>
                + {f.label}
              </button>
            ))}
          </div>
        </div>

        {/* Line palette */}
        <div style={{ border: "1px solid #e5e7eb", padding: 12, marginBottom: 12 }}>
          <div style={{ fontWeight: 600, marginBottom: 8 }}>Lines</div>
          <div style={{ display: "grid", gap: 6 }}>
            <button type="button" onClick={() => addLine("H")}
              style={{ textAlign: "left", padding: "8px 10px", cursor: "pointer", display: "flex", alignItems: "center", gap: 8 }}>
              {/* H line icon */}
              <span style={{ display: "inline-flex", alignItems: "center", width: 20, height: 16 }}>
                <span style={{ width: 20, height: 2, background: "#374151", borderRadius: 1 }} />
              </span>
              Add Horizontal Line
            </button>
            <button type="button" onClick={() => addLine("V")}
              style={{ textAlign: "left", padding: "8px 10px", cursor: "pointer", display: "flex", alignItems: "center", gap: 8 }}>
              {/* V line icon */}
              <span style={{ display: "inline-flex", justifyContent: "center", width: 20, height: 16 }}>
                <span style={{ width: 2, height: 16, background: "#374151", borderRadius: 1 }} />
              </span>
              Add Vertical Line
            </button>
          </div>
        </div>

        {/* Save / preview */}
        <div style={{ display: "grid", gap: 8 }}>
          <button type="button" onClick={onSave} style={{ padding: 10, cursor: "pointer" }}>
            Save Template
          </button>
          <div style={{ border: "1px solid #e5e7eb", padding: 12 }}>
            <div style={{ fontWeight: 600, marginBottom: 8 }}>Preview PDF</div>
            <input value={previewInvoiceId} onChange={e => setPreviewInvoiceId(e.target.value)}
              placeholder="InvoiceId GUID" style={{ width: "100%", padding: 8, marginBottom: 8 }} />
            <button type="button" onClick={onPreview} style={{ padding: 10, width: "100%", cursor: "pointer" }}>
              Preview (opens new tab)
            </button>
          </div>
        </div>

        {/* Raw JSON */}
        <div style={{ marginTop: 12 }}>
          <div style={{ fontWeight: 600, marginBottom: 6 }}>Raw JSON</div>
          <textarea value={rawJson} onChange={e => setRawJson(e.target.value)} rows={10}
            style={{ width: "100%", fontFamily: "monospace", fontSize: 12, padding: 8 }} />
          <button type="button" onClick={onApplyRawJson}
            style={{ padding: 10, width: "100%", cursor: "pointer" }}>
            Apply JSON
          </button>
        </div>
      </div>

      {/* ── Centre: canvas ────────────────────────────────────────────────── */}
      <div style={{ overflow: "auto", padding: 8, border: "1px solid #e5e7eb", background: "#fafafa" }}>
        <div
          ref={canvasRef}
          onPointerMove={onCanvasPointerMove}
          onPointerUp={onCanvasPointerUp}
          onPointerLeave={onCanvasPointerUp}
          onPointerDown={() => setSelectedId(null)}
          style={{
            width: pageW, height: pageH,
            background: "white", position: "relative", margin: "0 auto",
            boxShadow: "0 2px 8px rgba(0,0,0,0.08)",
            backgroundImage:
              "linear-gradient(to right,rgba(0,0,0,0.04) 1px,transparent 1px)," +
              "linear-gradient(to bottom,rgba(0,0,0,0.04) 1px,transparent 1px)",
            backgroundSize: `${12 * scale}px ${12 * scale}px`,
          }}
        >
          {/* Margin guide */}
          <div style={{
            position: "absolute",
            left:   template.page.margin * scale, top:    template.page.margin * scale,
            width:  (template.page.width  - template.page.margin * 2) * scale,
            height: (template.page.height - template.page.margin * 2) * scale,
            border: "1px dashed rgba(0,0,0,0.15)", pointerEvents: "none",
          }} />

          {/* Lines table block — clickable to select */}
          <div
            onPointerDown={e => { e.stopPropagation(); setSelectedId(LINES_TABLE_ID); }}
            style={{
              position: "absolute",
              left:   template.linesTable.x * scale, top:    template.linesTable.y * scale,
              width:  template.linesTable.w * scale, height: template.linesTable.h * scale,
              border: linesTableSelected
                ? "2px solid #7c3aed"
                : "1px dashed rgba(37,99,235,0.35)",
              background: linesTableSelected
                ? "rgba(124,58,237,0.06)"
                : "rgba(37,99,235,0.04)",
              cursor: "pointer",
              boxSizing: "border-box",
            }}
            title="Click to edit Lines Table properties"
          >
            <div style={{ fontSize: 11, fontWeight: 600, opacity: 0.65, padding: "4px 6px", color: linesTableSelected ? "#7c3aed" : "#2563eb" }}>
              Lines Table
            </div>
            {/* Column header labels preview */}
            <div style={{ display: "flex", paddingLeft: 6, gap: 0, pointerEvents: "none" }}>
              {template.linesTable.columns.map((col, i) => (
                <div key={i} style={{
                  width: col.w * scale, fontSize: 9, opacity: 0.7, overflow: "hidden",
                  textOverflow: "ellipsis", whiteSpace: "nowrap",
                  fontWeight: 600, color: "#374151",
                }}>
                  {col.header || col.key}
                </div>
              ))}
            </div>
          </div>

          {/* ── Render: horizontal/vertical lines ── */}
          {(template.lines ?? []).map(line => {
            const isSelected = line.id === selectedId;
            const thick      = Math.max(line.thickness * scale, 1);

            // Hit-area wrapper (wider than the visible line so it's easier to click)
            const hitPad = 6;

            if (line.orientation === "H") {
              return (
                <div
                  key={line.id}
                  title="Horizontal Line"
                  onPointerDown={e => onLinePointerDown(e, line.id, "move")}
                  style={{
                    position: "absolute",
                    left:   line.x * scale,
                    top:    line.y * scale - hitPad,
                    width:  line.length * scale,
                    height: thick + hitPad * 2,
                    cursor: "move",
                    zIndex: isSelected ? 10 : 5,
                    // Outline around hit area when selected
                    outline: isSelected ? "1.5px dashed #2563eb" : "none",
                    outlineOffset: 1,
                    boxSizing: "border-box",
                  }}
                >
                  {/* Visible line */}
                  <div style={{
                    position: "absolute",
                    left: 0, top: hitPad,
                    width: "100%", height: thick,
                    background: line.color,
                    borderRadius: thick > 2 ? 2 : 0,
                  }} />

                  {/* Resize handle — right end */}
                  {isSelected && (
                    <div
                      onPointerDown={e => onLinePointerDown(e, line.id, "resize")}
                      title="Drag to resize"
                      style={{
                        position: "absolute",
                        right: -5, top: "50%",
                        transform: "translateY(-50%)",
                        width: 10, height: 10,
                        background: "#2563eb",
                        border: "2px solid white",
                        borderRadius: "50%",
                        cursor: "ew-resize",
                        zIndex: 20,
                      }}
                    />
                  )}

                  {/* Selected label */}
                  {isSelected && (
                    <div style={{
                      position: "absolute", top: -18, left: 0,
                      fontSize: 10, color: "#2563eb", fontWeight: 600,
                      background: "white", padding: "1px 4px",
                      border: "1px solid #2563eb", borderRadius: 3,
                      whiteSpace: "nowrap", pointerEvents: "none",
                    }}>
                      H-Line · {Math.round(line.length)}pt
                    </div>
                  )}
                </div>
              );
            } else {
              // Vertical
              return (
                <div
                  key={line.id}
                  title="Vertical Line"
                  onPointerDown={e => onLinePointerDown(e, line.id, "move")}
                  style={{
                    position: "absolute",
                    left:   line.x * scale - hitPad,
                    top:    line.y * scale,
                    width:  thick + hitPad * 2,
                    height: line.length * scale,
                    cursor: "move",
                    zIndex: isSelected ? 10 : 5,
                    outline: isSelected ? "1.5px dashed #2563eb" : "none",
                    outlineOffset: 1,
                    boxSizing: "border-box",
                  }}
                >
                  {/* Visible line */}
                  <div style={{
                    position: "absolute",
                    left: hitPad, top: 0,
                    width: thick, height: "100%",
                    background: line.color,
                    borderRadius: thick > 2 ? 2 : 0,
                  }} />

                  {/* Resize handle — bottom end */}
                  {isSelected && (
                    <div
                      onPointerDown={e => onLinePointerDown(e, line.id, "resize")}
                      title="Drag to resize"
                      style={{
                        position: "absolute",
                        bottom: -5, left: "50%",
                        transform: "translateX(-50%)",
                        width: 10, height: 10,
                        background: "#2563eb",
                        border: "2px solid white",
                        borderRadius: "50%",
                        cursor: "ns-resize",
                        zIndex: 20,
                      }}
                    />
                  )}

                  {/* Selected label */}
                  {isSelected && (
                    <div style={{
                      position: "absolute", top: 0, left: 14,
                      fontSize: 10, color: "#2563eb", fontWeight: 600,
                      background: "white", padding: "1px 4px",
                      border: "1px solid #2563eb", borderRadius: 3,
                      whiteSpace: "nowrap", pointerEvents: "none",
                    }}>
                      V-Line · {Math.round(line.length)}pt
                    </div>
                  )}
                </div>
              );
            }
          })}

          {/* ── Render: field boxes ── */}
          {template.fields.map((f, idx) => {
            const fieldId    = f.id || `__missing_${idx}`;
            const isSelected = !!fieldId && fieldId === selectedId;
            const textColor  = f.color || DEFAULT_COLOR;
            return (
              <div
                key={fieldId}
                onPointerDown={e => onFieldPointerDown(e, fieldId, "move")}
                title={f.key}
                style={{
                  position: "absolute",
                  left:   f.x * scale, top:    f.y * scale,
                  width:  f.w * scale, height: f.h * scale,
                  border: isSelected ? "2px solid #2563eb" : "1px solid rgba(0,0,0,0.25)",
                  background: isSelected ? "rgba(37,99,235,0.06)" : "rgba(0,0,0,0.02)",
                  cursor: "move", boxSizing: "border-box", userSelect: "none", zIndex: isSelected ? 10 : 5,
                }}
              >
                <div style={{
                  fontFamily: f.font?.family ?? "monospace",
                  fontSize: Math.max(9, (f.font?.size ?? 11) * scale * 0.75),
                  fontWeight: f.font?.bold   ? "bold"   : "normal",
                  fontStyle:  f.font?.italic ? "italic" : "normal",
                  color: textColor, padding: 3, overflow: "hidden",
                  display: "flex", justifyContent: "space-between", alignItems: "center", gap: 4,
                }}>
                  <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{f.key}</span>
                  <span style={{ opacity: 0.5, fontSize: 9, flexShrink: 0 }}>{Math.round(f.x)},{Math.round(f.y)}</span>
                </div>
                {isSelected && (
                  <div
                    onPointerDown={e => onFieldPointerDown(e, fieldId, "resize")}
                    style={{
                      position: "absolute", right: 0, bottom: 0,
                      width: 14, height: 14, background: "#2563eb", cursor: "nwse-resize",
                    }}
                    title="Resize"
                  />
                )}
              </div>
            );
          })}
        </div>
      </div>

      {/* ── Right: properties ────────────────────────────────────────────── */}
      <div style={{ border: "1px solid #e5e7eb", padding: 12, overflowY: "auto" }}>
        <div style={{ fontWeight: 700, marginBottom: 8 }}>Properties</div>

        {/* Nothing selected */}
        {!selectedField && !selectedLine && !linesTableSelected && (
          <div style={{ opacity: 0.7 }}>Select a field, line, or the Lines Table on the page.</div>
        )}

        {/* ── Field properties ── */}
        {selectedField && (
          <>
            <div style={{
              marginBottom: 12, padding: "8px 10px",
              background: "#f9fafb", borderRadius: 6, border: "1px solid #e5e7eb",
            }}>
              <div style={{ fontWeight: 600, fontSize: 13 }}>{selectedField.key}</div>
              <div style={{ opacity: 0.5, fontSize: 11, fontFamily: "monospace" }}>{selectedField.id}</div>
            </div>

            <SectionLabel>Position &amp; Size</SectionLabel>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, marginBottom: 4 }}>
              <PropRow label="X">
                <input type="number" value={Math.round(selectedField.x)} style={inputStyle}
                  onChange={e => updateField(selectedField.id, { x: Number(e.target.value) })} />
              </PropRow>
              <PropRow label="Y">
                <input type="number" value={Math.round(selectedField.y)} style={inputStyle}
                  onChange={e => updateField(selectedField.id, { y: Number(e.target.value) })} />
              </PropRow>
              <PropRow label="Width">
                <input type="number" value={Math.round(selectedField.w)} style={inputStyle}
                  onChange={e => updateField(selectedField.id, { w: Number(e.target.value) })} />
              </PropRow>
              <PropRow label="Height">
                <input type="number" value={Math.round(selectedField.h)} style={inputStyle}
                  onChange={e => updateField(selectedField.id, { h: Number(e.target.value) })} />
              </PropRow>
            </div>

            <SectionLabel>Font</SectionLabel>
            <PropRow label="Family">
              <select
                value={selectedField.font.family}
                onChange={e => updateField(selectedField.id, { font: { ...selectedField.font, family: e.target.value } })}
                style={{ ...inputStyle, fontFamily: selectedField.font.family }}
              >
                {FONT_FAMILIES.map(ff => (
                  <option key={ff.value} value={ff.value} style={{ fontFamily: ff.value }}>{ff.label}</option>
                ))}
              </select>
            </PropRow>

            {/* Live font preview */}
            <div style={{
              margin: "8px 0", padding: "6px 10px",
              background: "#f9fafb", border: "1px solid #e5e7eb", borderRadius: 5,
              fontFamily: selectedField.font.family,
              fontSize: Math.min(selectedField.font.size, 18),
              fontWeight: selectedField.font.bold   ? "bold"   : "normal",
              fontStyle:  selectedField.font.italic ? "italic" : "normal",
              color: selectedField.color || DEFAULT_COLOR,
              whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis",
            }}>
              {selectedField.key} — The quick brown fox
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, marginBottom: 8 }}>
              <PropRow label="Size (pt)">
                <select
                  value={selectedField.font.size}
                  onChange={e => updateField(selectedField.id, { font: { ...selectedField.font, size: Number(e.target.value) } })}
                  style={inputStyle}
                >
                  {FONT_SIZES.map(s => <option key={s} value={s}>{s}</option>)}
                </select>
              </PropRow>
              <PropRow label="Align">
                <select
                  value={selectedField.align}
                  onChange={e => updateField(selectedField.id, { align: e.target.value as Align })}
                  style={inputStyle}
                >
                  {ALIGNMENTS.map(a => <option key={a} value={a}>{a}</option>)}
                </select>
              </PropRow>
            </div>

            <div style={{ display: "flex", gap: 14, marginBottom: 4 }}>
              <label style={{ display: "flex", gap: 6, alignItems: "center", cursor: "pointer" }}>
                <input type="checkbox" checked={selectedField.font.bold}
                  onChange={e => updateField(selectedField.id, { font: { ...selectedField.font, bold: e.target.checked } })} />
                <span style={{ fontWeight: "bold" }}>Bold</span>
              </label>
              <label style={{ display: "flex", gap: 6, alignItems: "center", cursor: "pointer" }}>
                <input type="checkbox" checked={selectedField.font.italic}
                  onChange={e => updateField(selectedField.id, { font: { ...selectedField.font, italic: e.target.checked } })} />
                <span style={{ fontStyle: "italic" }}>Italic</span>
              </label>
            </div>

            <SectionLabel>Text Colour</SectionLabel>
            <ColorPicker
              value={selectedField.color || DEFAULT_COLOR}
              onChange={hex => updateField(selectedField.id, { color: hex })}
            />

            <button type="button" onClick={() => removeField(selectedField.id)} style={removeBtn}>
              Remove Field
            </button>
          </>
        )}

        {/* ── Line properties ── */}
        {selectedLine && (
          <>
            <div style={{
              marginBottom: 12, padding: "8px 10px",
              background: "#f9fafb", borderRadius: 6, border: "1px solid #e5e7eb",
              display: "flex", alignItems: "center", gap: 10,
            }}>
              {/* Mini orientation icon */}
              <div style={{ width: 24, height: 24, display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
                {selectedLine.orientation === "H"
                  ? <div style={{ width: 20, height: 3, background: selectedLine.color, borderRadius: 1 }} />
                  : <div style={{ width: 3, height: 20, background: selectedLine.color, borderRadius: 1 }} />
                }
              </div>
              <div>
                <div style={{ fontWeight: 600, fontSize: 13 }}>
                  {selectedLine.orientation === "H" ? "Horizontal Line" : "Vertical Line"}
                </div>
                <div style={{ opacity: 0.5, fontSize: 11, fontFamily: "monospace" }}>{selectedLine.id}</div>
              </div>
            </div>

            <SectionLabel>Position</SectionLabel>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, marginBottom: 4 }}>
              <PropRow label="X">
                <input type="number" value={Math.round(selectedLine.x)} style={inputStyle}
                  onChange={e => updateLine(selectedLine.id, { x: Number(e.target.value) })} />
              </PropRow>
              <PropRow label="Y">
                <input type="number" value={Math.round(selectedLine.y)} style={inputStyle}
                  onChange={e => updateLine(selectedLine.id, { y: Number(e.target.value) })} />
              </PropRow>
            </div>

            <SectionLabel>Dimensions</SectionLabel>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, marginBottom: 4 }}>
              <PropRow label="Length (pt)">
                <input type="number" value={Math.round(selectedLine.length)} style={inputStyle}
                  onChange={e => updateLine(selectedLine.id, { length: Math.max(20, Number(e.target.value)) })} />
              </PropRow>
              <PropRow label="Thickness (pt)">
                <select
                  value={selectedLine.thickness}
                  onChange={e => updateLine(selectedLine.id, { thickness: Number(e.target.value) })}
                  style={inputStyle}
                >
                  {LINE_THICKNESSES.map(t => (
                    <option key={t} value={t}>{t}pt</option>
                  ))}
                </select>
              </PropRow>
            </div>

            {/* Live line preview */}
            <div style={{
              margin: "8px 0", padding: "10px 12px",
              background: "#f9fafb", border: "1px solid #e5e7eb", borderRadius: 5,
              display: "flex", alignItems: "center", justifyContent: "center", minHeight: 36,
            }}>
              {selectedLine.orientation === "H"
                ? <div style={{ width: "80%", height: selectedLine.thickness, background: selectedLine.color, borderRadius: 1 }} />
                : <div style={{ height: 30, width: selectedLine.thickness, background: selectedLine.color, borderRadius: 1 }} />
              }
            </div>

            <SectionLabel>Line Colour</SectionLabel>
            <ColorPicker
              value={selectedLine.color}
              onChange={hex => updateLine(selectedLine.id, { color: hex })}
            />

            <button type="button" onClick={() => removeLine(selectedLine.id)} style={removeBtn}>
              Remove Line
            </button>
          </>
        )}

        {/* ── LinesTable properties ── */}
        {linesTableSelected && (
          <>
            <div style={{
              marginBottom: 12, padding: "8px 10px",
              background: "#f5f3ff", borderRadius: 6, border: "1px solid #ddd6fe",
            }}>
              <div style={{ fontWeight: 600, fontSize: 13, color: "#7c3aed" }}>Lines Table</div>
              <div style={{ opacity: 0.55, fontSize: 11 }}>Invoice line items table</div>
            </div>

            <SectionLabel>Position &amp; Size</SectionLabel>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, marginBottom: 4 }}>
              <PropRow label="X">
                <input type="number" value={Math.round(template.linesTable.x)} style={inputStyle}
                  onChange={e => updateLinesTable({ x: Number(e.target.value) })} />
              </PropRow>
              <PropRow label="Y">
                <input type="number" value={Math.round(template.linesTable.y)} style={inputStyle}
                  onChange={e => updateLinesTable({ y: Number(e.target.value) })} />
              </PropRow>
              <PropRow label="Width">
                <input type="number" value={Math.round(template.linesTable.w)} style={inputStyle}
                  onChange={e => updateLinesTable({ w: Number(e.target.value) })} />
              </PropRow>
              <PropRow label="Height">
                <input type="number" value={Math.round(template.linesTable.h)} style={inputStyle}
                  onChange={e => updateLinesTable({ h: Number(e.target.value) })} />
              </PropRow>
            </div>

            {/* Column header labels */}
            <SectionLabel>Column Headers</SectionLabel>
            <div style={{ display: "grid", gap: 8 }}>
              {template.linesTable.columns.map((col, i) => (
                <div key={i} style={{
                  border: "1px solid #e5e7eb", borderRadius: 6, padding: "8px 10px",
                  background: "#fafafa",
                }}>
                  <div style={{ fontSize: 11, fontWeight: 600, color: "#6b7280", marginBottom: 6 }}>
                    Column {i + 1} · key: <code style={{ color: "#374151" }}>{col.key}</code>
                  </div>
                  <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 6 }}>
                    <PropRow label="Header Label">
                      <input
                        type="text"
                        value={col.header ?? col.key}
                        style={inputStyle}
                        onChange={e => updateColumn(i, { header: e.target.value })}
                        placeholder={col.key}
                      />
                    </PropRow>
                    <PropRow label="Align">
                      <select
                        value={col.align ?? "Left"}
                        style={inputStyle}
                        onChange={e => updateColumn(i, { align: e.target.value })}
                      >
                        {ALIGNMENTS.map(a => <option key={a} value={a}>{a}</option>)}
                      </select>
                    </PropRow>
                    <PropRow label="Width (pt)">
                      <input
                        type="number"
                        value={Math.round(col.w)}
                        style={inputStyle}
                        onChange={e => updateColumn(i, { w: Number(e.target.value) })}
                      />
                    </PropRow>
                  </div>
                </div>
              ))}
            </div>

            {/* Header font */}
            <SectionLabel>Header Font</SectionLabel>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, marginBottom: 6 }}>
              <PropRow label="Family">
                <select
                  value={template.linesTable.headerFont.family}
                  style={{ ...inputStyle, fontFamily: template.linesTable.headerFont.family }}
                  onChange={e => updateLinesTableFont("headerFont", { family: e.target.value })}
                >
                  {FONT_FAMILIES.map(ff => (
                    <option key={ff.value} value={ff.value} style={{ fontFamily: ff.value }}>{ff.label}</option>
                  ))}
                </select>
              </PropRow>
              <PropRow label="Size (pt)">
                <select
                  value={template.linesTable.headerFont.size}
                  style={inputStyle}
                  onChange={e => updateLinesTableFont("headerFont", { size: Number(e.target.value) })}
                >
                  {FONT_SIZES.map(s => <option key={s} value={s}>{s}</option>)}
                </select>
              </PropRow>
            </div>
            <div style={{ display: "flex", gap: 14, marginBottom: 4 }}>
              <label style={{ display: "flex", gap: 6, alignItems: "center", cursor: "pointer" }}>
                <input type="checkbox" checked={template.linesTable.headerFont.bold}
                  onChange={e => updateLinesTableFont("headerFont", { bold: e.target.checked })} />
                <span style={{ fontWeight: "bold" }}>Bold</span>
              </label>
              <label style={{ display: "flex", gap: 6, alignItems: "center", cursor: "pointer" }}>
                <input type="checkbox" checked={template.linesTable.headerFont.italic}
                  onChange={e => updateLinesTableFont("headerFont", { italic: e.target.checked })} />
                <span style={{ fontStyle: "italic" }}>Italic</span>
              </label>
            </div>

            {/* Row font */}
            <SectionLabel>Row Font</SectionLabel>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, marginBottom: 6 }}>
              <PropRow label="Family">
                <select
                  value={template.linesTable.rowFont.family}
                  style={{ ...inputStyle, fontFamily: template.linesTable.rowFont.family }}
                  onChange={e => updateLinesTableFont("rowFont", { family: e.target.value })}
                >
                  {FONT_FAMILIES.map(ff => (
                    <option key={ff.value} value={ff.value} style={{ fontFamily: ff.value }}>{ff.label}</option>
                  ))}
                </select>
              </PropRow>
              <PropRow label="Size (pt)">
                <select
                  value={template.linesTable.rowFont.size}
                  style={inputStyle}
                  onChange={e => updateLinesTableFont("rowFont", { size: Number(e.target.value) })}
                >
                  {FONT_SIZES.map(s => <option key={s} value={s}>{s}</option>)}
                </select>
              </PropRow>
            </div>
            <div style={{ display: "flex", gap: 14, marginBottom: 4 }}>
              <label style={{ display: "flex", gap: 6, alignItems: "center", cursor: "pointer" }}>
                <input type="checkbox" checked={template.linesTable.rowFont.bold}
                  onChange={e => updateLinesTableFont("rowFont", { bold: e.target.checked })} />
                <span style={{ fontWeight: "bold" }}>Bold</span>
              </label>
              <label style={{ display: "flex", gap: 6, alignItems: "center", cursor: "pointer" }}>
                <input type="checkbox" checked={template.linesTable.rowFont.italic}
                  onChange={e => updateLinesTableFont("rowFont", { italic: e.target.checked })} />
                <span style={{ fontStyle: "italic" }}>Italic</span>
              </label>
            </div>
          </>
        )}

        <div style={{ marginTop: 16, opacity: 0.7, fontSize: 12 }}>
          Notes:
          <ul style={{ marginTop: 6, paddingLeft: 18 }}>
            <li>Coordinates are in PDF points (A4 is 595×842).</li>
            <li>Drag any element to move it; drag the blue handle to resize.</li>
            <li>Lines are stored in the <code>lines</code> array in the JSON.</li>
            <li>Your PDFsharp renderer needs to read <code>lines</code> and draw them.</li>
          </ul>
        </div>
      </div>
    </div>
  );
}
