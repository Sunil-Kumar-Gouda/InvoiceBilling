export const TOKEN_KEY = "invoicebilling.accessToken";
export const TOKEN_CHANGED_EVENT = "invoicebilling:token-changed";

function notifyTokenChanged(token: string | null): void {
  try {
    // Custom event so same-tab updates can sync auth state.
    window.dispatchEvent(new CustomEvent(TOKEN_CHANGED_EVENT, { detail: { token } }));
  } catch {
    // ignore (e.g., server-side render or restricted environment)
  }
}

export function getAccessToken(): string | null {
  try {
    const t = localStorage.getItem(TOKEN_KEY);
    return t && t.trim().length > 0 ? t : null;
  } catch {
    return null;
  }
}

export function setAccessToken(token: string | null): void {
  try {
    if (!token) {
      localStorage.removeItem(TOKEN_KEY);
      notifyTokenChanged(null);
      return;
    }
    localStorage.setItem(TOKEN_KEY, token);
    notifyTokenChanged(token);
  } catch {
    // ignore (e.g., privacy mode)
  }
}
