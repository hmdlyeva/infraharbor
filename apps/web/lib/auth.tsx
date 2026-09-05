"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080";
const REFRESH_LEEWAY_MS = 30_000;

type AuthStatus = "loading" | "authenticated" | "anonymous";

export type AuthUser = {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
};

type AuthResponse = {
  tokenType: "Bearer";
  accessToken: string;
  accessTokenExpiresAt: string;
  user: AuthUser;
};

type AuthContextValue = {
  status: AuthStatus;
  user: AuthUser | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshSession: () => Promise<boolean>;
  authenticatedFetch: (path: string, init?: RequestInit) => Promise<Response>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

function apiUrl(path: string) {
  return `${API_BASE_URL}${path.startsWith("/") ? path : `/${path}`}`;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>("loading");
  const [user, setUser] = useState<AuthUser | null>(null);
  const [expiresAt, setExpiresAt] = useState<number | null>(null);
  const accessTokenRef = useRef<string | null>(null);
  const refreshPromiseRef = useRef<Promise<boolean> | null>(null);

  const clearSession = useCallback(() => {
    accessTokenRef.current = null;
    setUser(null);
    setExpiresAt(null);
    setStatus("anonymous");
  }, []);

  const applySession = useCallback((payload: AuthResponse) => {
    accessTokenRef.current = payload.accessToken;
    setUser(payload.user);
    setExpiresAt(Date.parse(payload.accessTokenExpiresAt));
    setStatus("authenticated");
  }, []);

  const refreshSession = useCallback(async () => {
    if (refreshPromiseRef.current) {
      return refreshPromiseRef.current;
    }

    const refreshPromise = (async () => {
      try {
        const response = await fetch(apiUrl("/api/auth/refresh"), {
          method: "POST",
          credentials: "include",
          cache: "no-store",
        });

        if (!response.ok) {
          clearSession();
          return false;
        }

        const payload = (await response.json()) as AuthResponse;
        applySession(payload);
        return true;
      } catch {
        clearSession();
        return false;
      } finally {
        refreshPromiseRef.current = null;
      }
    })();

    refreshPromiseRef.current = refreshPromise;
    return refreshPromise;
  }, [applySession, clearSession]);

  useEffect(() => {
    void refreshSession();
  }, [refreshSession]);

  useEffect(() => {
    if (status !== "authenticated" || expiresAt === null || Number.isNaN(expiresAt)) {
      return;
    }

    const delay = Math.max(1_000, expiresAt - Date.now() - REFRESH_LEEWAY_MS);
    const timeout = window.setTimeout(() => {
      void refreshSession();
    }, delay);

    return () => window.clearTimeout(timeout);
  }, [expiresAt, refreshSession, status]);

  const login = useCallback(
    async (email: string, password: string) => {
      const response = await fetch(apiUrl("/api/auth/login"), {
        method: "POST",
        credentials: "include",
        cache: "no-store",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });

      if (!response.ok) {
        clearSession();
        throw new Error("authentication_failed");
      }

      const payload = (await response.json()) as AuthResponse;
      applySession(payload);
    },
    [applySession, clearSession],
  );

  const logout = useCallback(async () => {
    try {
      await fetch(apiUrl("/api/auth/logout"), {
        method: "POST",
        credentials: "include",
        cache: "no-store",
      });
    } finally {
      clearSession();
    }
  }, [clearSession]);

  const authenticatedFetch = useCallback(
    async (path: string, init?: RequestInit) => {
      let token = accessTokenRef.current;
      if (!token && !(await refreshSession())) {
        return new Response(null, { status: 401 });
      }

      token = accessTokenRef.current;
      const send = (bearer: string) => {
        const headers = new Headers(init?.headers);
        headers.set("Authorization", `Bearer ${bearer}`);
        return fetch(apiUrl(path), {
          ...init,
          credentials: "include",
          headers,
        });
      };

      let response = await send(token!);
      if (response.status !== 401) {
        return response;
      }

      if (!(await refreshSession()) || !accessTokenRef.current) {
        clearSession();
        return response;
      }

      response = await send(accessTokenRef.current);
      if (response.status === 401) {
        clearSession();
      }

      return response;
    },
    [clearSession, refreshSession],
  );

  const value = useMemo<AuthContextValue>(
    () => ({ status, user, login, logout, refreshSession, authenticatedFetch }),
    [authenticatedFetch, login, logout, refreshSession, status, user],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used inside AuthProvider");
  }

  return context;
}
