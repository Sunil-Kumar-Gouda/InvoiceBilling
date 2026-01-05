import type { ProblemDetails } from "./types";
import { ApiError } from "./types";

export type ErrorInfo = {
  status?: number;
  title?: string;
  message: string;
  lines?: string[];
  kind: "validation" | "conflict" | "other";
};

function isStringArray(x: unknown): x is string[] {
  return Array.isArray(x) && x.every(v => typeof v === "string");
}

function readValidationErrors(problem?: ProblemDetails): string[] {
  if (!problem) return [];

  // Common ASP.NET Core validation shape: { errors: { Field: ["msg1", "msg2"], ... } }
  const errors = problem["errors"];
  if (!errors || typeof errors !== "object") return [];

  const msgs: string[] = [];
  for (const [key, value] of Object.entries(errors as Record<string, unknown>))
  {
    if (typeof value === "string") {
      msgs.push(`${key}: ${value}`);
      continue;
    }
    if (isStringArray(value)) {
      for (const m of value) msgs.push(`${key}: ${m}`);
    }
  }
  return msgs;
}

function splitDetailLines(detail?: string): string[] {
  if (!detail) return [];
  return detail
    .split(/\r?\n/)
    .map(s => s.trim())
    .filter(Boolean);
}

/**
 * Formats unknown errors into a predictable shape for UI.
 *
 * - ApiError: uses ProblemDetails when present
 * - ASP.NET validation: uses problem.errors (dictionary) if present
 * - Falls back to message text
 */
export function formatError(e: unknown): ErrorInfo {
  if (e instanceof ApiError) {
    const status = e.status;
    const title = e.problemDetails?.title;
    const detail = e.problemDetails?.detail;

    const validationLines = readValidationErrors(e.problemDetails);
    const detailLines = splitDetailLines(detail);

    const lines = validationLines.length > 0 ? validationLines : detailLines;

    const message = detail ?? title ?? e.message;
    const kind: ErrorInfo["kind"] =
      status === 409 ? "conflict" :
      status === 400 ? "validation" :
      "other";

    return { status, title, message, lines: lines.length ? lines : undefined, kind };
  }

  if (e instanceof Error) {
    return { message: e.message, kind: "other" };
  }

  return { message: "Unexpected error", kind: "other" };
}
