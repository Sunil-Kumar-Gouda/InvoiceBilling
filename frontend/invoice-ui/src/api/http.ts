import { API_BASE_URL } from "../config";
import type { ProblemDetails } from "./types";
import { ApiError } from "./types";

function buildUrl(path: string): string {
  const base = API_BASE_URL.endsWith("/") ? API_BASE_URL.slice(0, -1) : API_BASE_URL;
  const p = path.startsWith("/") ? path : `/${path}`;
  return `${base}${p}`;
}

function shouldSendJson(init?: RequestInit): boolean {
  const method = (init?.method ?? "GET").toUpperCase();
  if (!["POST", "PUT", "PATCH", "DELETE"].includes(method)) return false;
  if (init?.body == null) return false;

  // We only auto-set JSON content-type when body is a string (your code uses JSON.stringify)
  return typeof init.body === "string";
}

function looksLikeProblemDetails(x: unknown): x is ProblemDetails {
  if (!x || typeof x !== "object") return false;
  const obj = x as Record<string, unknown>;
  return "title" in obj || "detail" in obj || "status" in obj || "type" in obj;
}

async function tryReadProblemDetails(res: Response): Promise<ProblemDetails | null> {
  const ct = (res.headers.get("content-type") ?? "").toLowerCase();

  // ASP.NET Core ProblemDetails is typically application/problem+json
  // Some APIs may still return application/json for problem payloads.
  if (!ct.includes("application/problem+json") && !ct.includes("application/json")) return null;

  try {
    const json = (await res.json()) as unknown;
    return looksLikeProblemDetails(json) ? (json as ProblemDetails) : null;
  } catch {
    return null;
  }
}

async function throwApiError(res: Response): Promise<never> {
  const problem = await tryReadProblemDetails(res);

  let rawBody: string | undefined;
  if (!problem) {
    try {
      rawBody = await res.text();
    } catch {
      rawBody = undefined;
    }
  }

  const message = problem?.detail ?? problem?.title ?? rawBody ?? `HTTP ${res.status}`;

  throw new ApiError({
    message,
    status: res.status,
    problemDetails: problem ?? undefined,
    rawBody,
  });
}

/**
 * JSON client: returns parsed JSON on success.
 * Throws ApiError with ProblemDetails when server returns problem+json.
 */
export async function http<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);

  if (!headers.has("Accept")) headers.set("Accept", "application/json");
  if (shouldSendJson(init) && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");

  const res = await fetch(buildUrl(path), { ...init, headers });

  if (!res.ok) {
    await throwApiError(res);
  }

  if (res.status === 204) {
    return undefined as T;
  }

  // For safety: if server returns non-json even on OK, return text as T
  const ct = (res.headers.get("content-type") ?? "").toLowerCase();
  if (!ct.includes("application/json") && !ct.includes("application/problem+json")) {
    const text = await res.text();
    return (text as unknown) as T;
  }

  return (await res.json()) as T;
}

/**
 * Blob client for downloads (PDFs, files, etc.).
 * Still throws ApiError with ProblemDetails parsed if server returns problem+json.
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
