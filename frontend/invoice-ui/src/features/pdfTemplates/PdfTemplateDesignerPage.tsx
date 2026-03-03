import React, { useEffect, useMemo, useRef, useState } from "react";
import ErrorBanner from "../../components/ErrorBanner";
import { formatError, type ErrorInfo } from "../../api/errorFormat";
import { getActivePdfTemplate, previewPdfTemplate, saveActivePdfTemplate } from "../../api/pdfTemplatesApi";
import {
 // A4,
  AVAILABLE_FIELDS,
  defaultTemplate,
  newId,
  type Align,
  type FieldKey,
  type FieldPlacement,
  type PdfTemplateDefinition,
} from "./types";

type DragMode = "move" | "resize";

type DragState = {
  mode: DragMode;
  fieldId: string;
  startX: number;
  startY: number;
  origX: number;
  origY: number;
  origW: number;
  origH: number;
};

const FONT_FAMILIES = ["Roboto", "Helvetica", "Arial", "Times New Roman"];
const ALIGNMENTS: Align[] = ["Left", "Center", "Right"];

function clamp(n: number, min: number, max: number) {
  return Math.max(min, Math.min(max, n));
}

function downloadOrOpenPdf(blob: Blob) {
  const url = URL.createObjectURL(blob);
  window.open(url, "_blank", "noopener,noreferrer");
  // NOTE: not revoking immediately so tab can load. You can revoke later via setTimeout if you want.
}

export default function PdfTemplateDesignerPage() {
  const [loading, setLoading] = useState(true);
  const [template, setTemplate] = useState<PdfTemplateDefinition>(() => defaultTemplate());
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [scale, setScale] = useState(1.0); // screen px per PDF point
  const [error, setError] = useState<ErrorInfo | null>(null);

  const [previewInvoiceId, setPreviewInvoiceId] = useState("");
  const [rawJson, setRawJson] = useState<string>("");

  const dragRef = useRef<DragState | null>(null);
  const canvasRef = useRef<HTMLDivElement | null>(null);

  const pageW = template.page.width * scale;
  const pageH = template.page.height * scale;

  const selectedField = useMemo(() => {
    if (!selectedId) return null;
    return template.fields.find(f => f.id === selectedId) ?? null;
  }, [template.fields, selectedId]);

  useEffect(() => {
    (async () => {
      try {
        setLoading(true);
        const existing = await getActivePdfTemplate();
        setTemplate(existing ?? defaultTemplate());
      } catch (e) {
        setError(formatError(e));
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  useEffect(() => {
    // keep raw json in sync for copy/edit
    setRawJson(JSON.stringify(template, null, 2));
  }, [template]);

  function updateField(fieldId: string, patch: Partial<FieldPlacement>) {
    setTemplate(prev => ({
      ...prev,
      fields: prev.fields.map(f => (f.id === fieldId ? { ...f, ...patch } : f)),
    }));
  }

  function removeField(fieldId: string) {
    setTemplate(prev => ({
      ...prev,
      fields: prev.fields.filter(f => f.id !== fieldId),
    }));
    if (selectedId === fieldId) setSelectedId(null);
  }

  function addField(key: FieldKey) {
    const meta = AVAILABLE_FIELDS.find(x => x.key === key)!;
    const margin = template.page.margin;
    const nextY = clamp(
      margin + 20 + template.fields.length * 18,
      margin,
      template.page.height - margin - meta.defaultH
    );

    const f: FieldPlacement = {
      id: newId("f"),
      key,
      x: margin,
      y: nextY,
      w: meta.defaultW,
      h: meta.defaultH,
      align: "Left",
      font: { family: "Roboto", size: 11, bold: false, italic: false },
    };

    setTemplate(prev => ({ ...prev, fields: [...prev.fields, f] }));
    setSelectedId(f.id);
  }

  function getLocalPoint(e: PointerEvent | React.PointerEvent) {
    const canvas = canvasRef.current;
    if (!canvas) return { x: 0, y: 0 };

    const r = canvas.getBoundingClientRect();
    const xPx = (e as any).clientX - r.left;
    const yPx = (e as any).clientY - r.top;
    return { x: xPx / scale, y: yPx / scale };
  }

  function onFieldPointerDown(e: React.PointerEvent, fieldId: string, mode: DragMode) {
    e.preventDefault();
    e.stopPropagation();

    const f = template.fields.find(x => x.id === fieldId);
    if (!f) return;

    setSelectedId(fieldId);

    const pt = getLocalPoint(e);
    dragRef.current = {
      mode,
      fieldId,
      startX: pt.x,
      startY: pt.y,
      origX: f.x,
      origY: f.y,
      origW: f.w,
      origH: f.h,
    };

    // capture pointer so we keep getting move events
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
  }

  function onCanvasPointerMove(e: React.PointerEvent) {
    const drag = dragRef.current;
    if (!drag) return;

    const pt = getLocalPoint(e);
    const dx = pt.x - drag.startX;
    const dy = pt.y - drag.startY;

    const margin = template.page.margin;

    if (drag.mode === "move") {
      const newX = clamp(drag.origX + dx, margin, template.page.width - margin - drag.origW);
      const newY = clamp(drag.origY + dy, margin, template.page.height - margin - drag.origH);
      updateField(drag.fieldId, { x: newX, y: newY });
    } else {
      const minW = 40;
      const minH = 12;
      const newW = clamp(drag.origW + dx, minW, template.page.width - margin - drag.origX);
      const newH = clamp(drag.origH + dy, minH, template.page.height - margin - drag.origY);
      updateField(drag.fieldId, { w: newW, h: newH });
    }
  }

  function onCanvasPointerUp() {
    dragRef.current = null;
  }

  async function onSave() {
    try {
      setError(null);
      await saveActivePdfTemplate(template);
      alert("Saved template.");
    } catch (e) {
      setError(formatError(e));
    }
  }

  async function onPreview() {
    try {
      setError(null);
      if (!previewInvoiceId || previewInvoiceId.trim().length === 0) {
        setError({
          kind: "validation",
          title: "Preview needs an invoice",
          message: "Enter an existing InvoiceId (GUID) to preview PDF rendering.",
          status: 400,
          lines: [],
        });
        return;
      }

      const blob = await previewPdfTemplate(template, previewInvoiceId.trim());
      downloadOrOpenPdf(blob);
    } catch (e) {
      setError(formatError(e));
    }
  }

  function onApplyRawJson() {
    try {
      setError(null);
      const parsed = JSON.parse(rawJson) as PdfTemplateDefinition;

      // basic sanity checks
      if (!parsed?.page?.width || !parsed?.page?.height || !Array.isArray(parsed.fields)) {
        throw new Error("Invalid template JSON structure.");
      }
      setTemplate(parsed);
      alert("Applied JSON.");
    } catch (e: any) {
      setError({
        kind: "validation",
        title: "Invalid JSON",
        message: e?.message ?? "Invalid JSON",
        status: 400,
        lines: [],
      });
    }
  }

  if (loading) return <div style={{ padding: 16 }}>Loading...</div>;

  return (
    <div style={{ padding: 16, display: "grid", gridTemplateColumns: "260px 1fr 320px", gap: 16 }}>
      {/* Left: field palette */}
      <div>
        <h2 style={{ marginTop: 0 }}>PDF Designer</h2>

        {error && <ErrorBanner error={error} onDismiss={() => setError(null)} />}

        <div style={{ marginBottom: 12 }}>
          <label style={{ display: "block", fontWeight: 600 }}>Scale</label>
          <input
            type="range"
            min={0.6}
            max={1.6}
            step={0.1}
            value={scale}
            onChange={e => setScale(Number(e.target.value))}
          />
          <div style={{ opacity: 0.75 }}>{scale.toFixed(1)}×</div>
        </div>

        <div style={{ border: "1px solid #e5e7eb", padding: 12 }}>
          <div style={{ fontWeight: 600, marginBottom: 8 }}>Fields</div>
          <div style={{ display: "grid", gap: 6 }}>
            {AVAILABLE_FIELDS.map(f => (
              <button
                key={f.key}
                type="button"
                onClick={() => addField(f.key)}
                style={{ textAlign: "left", padding: "8px 10px", cursor: "pointer" }}
              >
                + {f.label}
              </button>
            ))}
          </div>
        </div>

        <div style={{ marginTop: 12, display: "grid", gap: 8 }}>
          <button type="button" onClick={onSave} style={{ padding: 10, cursor: "pointer" }}>
            Save Template
          </button>

          <div style={{ border: "1px solid #e5e7eb", padding: 12 }}>
            <div style={{ fontWeight: 600, marginBottom: 8 }}>Preview PDF</div>
            <input
              value={previewInvoiceId}
              onChange={e => setPreviewInvoiceId(e.target.value)}
              placeholder="InvoiceId GUID"
              style={{ width: "100%", padding: 8, marginBottom: 8 }}
            />
            <button type="button" onClick={onPreview} style={{ padding: 10, width: "100%", cursor: "pointer" }}>
              Preview (opens new tab)
            </button>
          </div>
        </div>

        <div style={{ marginTop: 12 }}>
          <div style={{ fontWeight: 600, marginBottom: 6 }}>Raw JSON</div>
          <textarea
            value={rawJson}
            onChange={e => setRawJson(e.target.value)}
            rows={10}
            style={{ width: "100%", fontFamily: "monospace", fontSize: 12, padding: 8 }}
          />
          <button type="button" onClick={onApplyRawJson} style={{ padding: 10, width: "100%", cursor: "pointer" }}>
            Apply JSON
          </button>
        </div>
      </div>

      {/* Center: A4 canvas */}
      <div style={{ overflow: "auto", padding: 8, border: "1px solid #e5e7eb", background: "#fafafa" }}>
        <div
          ref={canvasRef}
          onPointerMove={onCanvasPointerMove}
          onPointerUp={onCanvasPointerUp}
          onPointerLeave={onCanvasPointerUp}
          onPointerDown={() => setSelectedId(null)}
          style={{
            width: pageW,
            height: pageH,
            background: "white",
            position: "relative",
            margin: "0 auto",
            boxShadow: "0 2px 8px rgba(0,0,0,0.08)",
            backgroundImage:
              "linear-gradient(to right, rgba(0,0,0,0.04) 1px, transparent 1px), linear-gradient(to bottom, rgba(0,0,0,0.04) 1px, transparent 1px)",
            backgroundSize: `${12 * scale}px ${12 * scale}px`,
          }}
        >
          {/* Margin guide */}
          <div
            style={{
              position: "absolute",
              left: template.page.margin * scale,
              top: template.page.margin * scale,
              width: (template.page.width - template.page.margin * 2) * scale,
              height: (template.page.height - template.page.margin * 2) * scale,
              border: "1px dashed rgba(0,0,0,0.15)",
              pointerEvents: "none",
            }}
          />

          {/* Lines table placeholder block (optional to move later; for now just show) */}
          <div
            style={{
              position: "absolute",
              left: template.linesTable.x * scale,
              top: template.linesTable.y * scale,
              width: template.linesTable.w * scale,
              height: template.linesTable.h * scale,
              border: "1px dashed rgba(37,99,235,0.35)",
              background: "rgba(37,99,235,0.04)",
              pointerEvents: "none",
            }}
            title="Lines Table Area"
          >
            <div style={{ fontSize: 12, opacity: 0.7, padding: 6 }}>LinesTable</div>
          </div>

          {template.fields.map(f => {
            const isSelected = f.id === selectedId;
            return (
              <div
                key={f.id}
                style={{
                  position: "absolute",
                  left: f.x * scale,
                  top: f.y * scale,
                  width: f.w * scale,
                  height: f.h * scale,
                  border: isSelected ? "2px solid #2563eb" : "1px solid rgba(0,0,0,0.25)",
                  background: isSelected ? "rgba(37,99,235,0.06)" : "rgba(0,0,0,0.02)",
                  cursor: "move",
                  boxSizing: "border-box",
                  userSelect: "none",
                }}
                onPointerDown={e => onFieldPointerDown(e, f.id, "move")}
                title={f.key}
              >
                <div
                  style={{
                    fontFamily: "monospace",
                    fontSize: 11,
                    padding: 4,
                    opacity: 0.85,
                    display: "flex",
                    justifyContent: "space-between",
                    gap: 8,
                  }}
                >
                  <span>{f.key}</span>
                  <span style={{ opacity: 0.6 }}>
                    {Math.round(f.x)},{Math.round(f.y)}
                  </span>
                </div>

                {isSelected && (
                  <div
                    onPointerDown={e => onFieldPointerDown(e, f.id, "resize")}
                    style={{
                      position: "absolute",
                      right: 0,
                      bottom: 0,
                      width: 14,
                      height: 14,
                      background: "#2563eb",
                      cursor: "nwse-resize",
                    }}
                    title="Resize"
                  />
                )}
              </div>
            );
          })}
        </div>
      </div>

      {/* Right: properties */}
      <div style={{ border: "1px solid #e5e7eb", padding: 12 }}>
        <div style={{ fontWeight: 700, marginBottom: 8 }}>Properties</div>

        {!selectedField ? (
          <div style={{ opacity: 0.7 }}>Select a field on the page.</div>
        ) : (
          <>
            <div style={{ marginBottom: 10 }}>
              <div style={{ fontWeight: 600 }}>{selectedField.key}</div>
              <div style={{ opacity: 0.7, fontSize: 12 }}>{selectedField.id}</div>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
              <label>
                X
                <input
                  type="number"
                  value={selectedField.x}
                  onChange={e => updateField(selectedField.id, { x: Number(e.target.value) })}
                  style={{ width: "100%", padding: 6 }}
                />
              </label>
              <label>
                Y
                <input
                  type="number"
                  value={selectedField.y}
                  onChange={e => updateField(selectedField.id, { y: Number(e.target.value) })}
                  style={{ width: "100%", padding: 6 }}
                />
              </label>
              <label>
                W
                <input
                  type="number"
                  value={selectedField.w}
                  onChange={e => updateField(selectedField.id, { w: Number(e.target.value) })}
                  style={{ width: "100%", padding: 6 }}
                />
              </label>
              <label>
                H
                <input
                  type="number"
                  value={selectedField.h}
                  onChange={e => updateField(selectedField.id, { h: Number(e.target.value) })}
                  style={{ width: "100%", padding: 6 }}
                />
              </label>
            </div>

            <div style={{ marginTop: 10 }}>
              <label style={{ display: "block" }}>
                Font family
                <select
                  value={selectedField.font.family}
                  onChange={e =>
                    updateField(selectedField.id, { font: { ...selectedField.font, family: e.target.value } })
                  }
                  style={{ width: "100%", padding: 6 }}
                >
                  {FONT_FAMILIES.map(ff => (
                    <option key={ff} value={ff}>
                      {ff}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            <div style={{ marginTop: 10, display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
              <label>
                Size
                <input
                  type="number"
                  value={selectedField.font.size}
                  onChange={e =>
                    updateField(selectedField.id, { font: { ...selectedField.font, size: Number(e.target.value) } })
                  }
                  style={{ width: "100%", padding: 6 }}
                />
              </label>

              <label>
                Align
                <select
                  value={selectedField.align}
                  onChange={e => updateField(selectedField.id, { align: e.target.value as Align })}
                  style={{ width: "100%", padding: 6 }}
                >
                  {ALIGNMENTS.map(a => (
                    <option key={a} value={a}>
                      {a}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            <div style={{ marginTop: 10, display: "flex", gap: 10 }}>
              <label style={{ display: "flex", gap: 6, alignItems: "center" }}>
                <input
                  type="checkbox"
                  checked={selectedField.font.bold}
                  onChange={e =>
                    updateField(selectedField.id, { font: { ...selectedField.font, bold: e.target.checked } })
                  }
                />
                Bold
              </label>

              <label style={{ display: "flex", gap: 6, alignItems: "center" }}>
                <input
                  type="checkbox"
                  checked={selectedField.font.italic}
                  onChange={e =>
                    updateField(selectedField.id, { font: { ...selectedField.font, italic: e.target.checked } })
                  }
                />
                Italic
              </label>
            </div>

            <div style={{ marginTop: 12, display: "grid", gap: 8 }}>
              <button
                type="button"
                onClick={() => removeField(selectedField.id)}
                style={{ padding: 10, cursor: "pointer" }}
              >
                Remove field
              </button>
            </div>
          </>
        )}

        <div style={{ marginTop: 16, opacity: 0.75, fontSize: 12 }}>
          Notes:
          <ul style={{ marginTop: 6, paddingLeft: 18 }}>
            <li>Coordinates are in PDF points (A4 is 595×842).</li>
            <li>This only edits field layout JSON. Your PDFsharp worker will render using it.</li>
          </ul>
        </div>
      </div>
    </div>
  );
}
