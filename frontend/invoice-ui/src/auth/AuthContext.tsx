import React, { createContext, useContext, useEffect, useMemo, useState } from "react";
import { getAccessToken, setAccessToken, TOKEN_CHANGED_EVENT, TOKEN_KEY } from "./tokenStorage";

type AuthContextValue = {
  token: string | null;
  isAuthenticated: boolean;
  setToken: (token: string | null) => void;
  logout: () => void;
};

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

type Props = {
  children: React.ReactNode;
};

export function AuthProvider({ children }: Props) {
  const [token, setTokenState] = useState<string | null>(() => getAccessToken());

  // Keep auth state in sync across same-tab calls (custom event) and other tabs (storage event).
  useEffect(() => {
    const sync = () => setTokenState(getAccessToken());

    const onTokenChanged = () => sync();
    const onStorage = (e: StorageEvent) => {
      if (e.key === TOKEN_KEY) sync();
    };

    window.addEventListener(TOKEN_CHANGED_EVENT, onTokenChanged as EventListener);
    window.addEventListener("storage", onStorage);

    return () => {
      window.removeEventListener(TOKEN_CHANGED_EVENT, onTokenChanged as EventListener);
      window.removeEventListener("storage", onStorage);
    };
  }, []);

  const setToken = (t: string | null) => {
    setTokenState(t);
    setAccessToken(t);
  };

  const logout = () => setToken(null);

  const value = useMemo<AuthContextValue>(() => {
    return {
      token,
      isAuthenticated: !!token,
      setToken,
      logout,
    };
  }, [token]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth must be used within <AuthProvider>");
  }
  return ctx;
}
