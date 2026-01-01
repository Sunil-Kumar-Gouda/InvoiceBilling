import { API_BASE_URL } from "../config";
import type { ProblemDetails  } from "./types";
import { ApiError  } from "./types";

function buildUrl(path: string): string {
  return `${API_BASE_URL}${path}`;
}

function hasJsonBody(init?: RequestInit): boolean {
  const method = (init?.method ?? "GET").toUpperCase();
  // Most APIs only send JSON bodies on these methods
  return ["POST", "PUT", "PATCH"].includes(method) && init?.body != null;
}

async function tryReadProblemDetails(res: Response): Promise<ProblemDetails | null> {
  const ct = res.headers.get("content-type") ?? "";
  if (!ct.includes("application/problem+json")) return null;

  try {
    return (await res.json()) as ProblemDetails;
  } catch {
    return null;
  }
}

async function throwApiError(res: Response): Promise<never> {
  const problem = await tryReadProblemDetails(res);

  // If it wasn’t problem+json, try plain text (best-effort)
  let raw = "";
  if (!problem) {
    try {
      raw = await res.text();
    } catch {
      raw = "";
    }
  }

  const message =
    (problem?.detail as string | undefined) ||
    (problem?.title as string | undefined) ||
    raw ||
    `HTTP ${res.status}`;

  throw new ApiError({
    message,
    status: res.status,
    problemDetails: problem ?? undefined,
    rawBody: raw || undefined,
  });
}

/**
 * JSON client (default for typical APIs).
 * - Adds Content-Type: application/json only when a body exists and caller didn't already set it.
 */
export async function http<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers ?? {});
  if (hasJsonBody(init) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const res = await fetch(buildUrl(path), { ...init, headers });

  if (!res.ok) {
    await throwApiError(res);
  }

  if (res.status === 204) return undefined as T;

  return (await res.json()) as T;
}

/**
 * Blob client for downloads (PDFs, files, etc.).
 * It still throws ApiError with ProblemDetails parsed if server returns problem+json.
 */
export async function httpBlob(
  path: string,
  init?: RequestInit
): Promise<{ blob: Blob; headers: Headers }> {
  const res = await fetch(buildUrl(path), init);

  if (!res.ok) {
    await throwApiError(res);
  }

  return { blob: await res.blob(), headers: res.headers };
}
