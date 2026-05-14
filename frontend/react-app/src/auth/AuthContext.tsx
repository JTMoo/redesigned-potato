import { createContext, useContext, useState, useCallback } from "react";
import { useNavigate } from "react-router-dom";

interface JwtPayload {
  sub: string;
  email: string;
  name: string;
  exp: number;
}

interface AuthUser {
  id: string;
  email: string;
  name: string;
}

interface AuthContextValue {
  user: AuthUser | null;
  token: string | null;
  login: (token: string) => void;
  logout: () => void;
}

function decodeJwt(token: string): JwtPayload {
  const base64 = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
  return JSON.parse(atob(base64)) as JwtPayload;
}

function loadStoredAuth(): { user: AuthUser | null; token: string | null } {
  const token = localStorage.getItem("auth_token");
  if (!token) return { user: null, token: null };
  try {
    const payload = decodeJwt(token);
    if (payload.exp * 1000 < Date.now()) {
      localStorage.removeItem("auth_token");
      return { user: null, token: null };
    }
    return { user: { id: payload.sub, email: payload.email, name: payload.name }, token };
  } catch {
    localStorage.removeItem("auth_token");
    return { user: null, token: null };
  }
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const stored = loadStoredAuth();
  const [user, setUser] = useState<AuthUser | null>(stored.user);
  const [token, setToken] = useState<string | null>(stored.token);
  const navigate = useNavigate();

  const login = useCallback((newToken: string) => {
    const payload = decodeJwt(newToken);
    localStorage.setItem("auth_token", newToken);
    setToken(newToken);
    setUser({ id: payload.sub, email: payload.email, name: payload.name });
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem("auth_token");
    setToken(null);
    setUser(null);
    navigate("/login");
  }, [navigate]);

  return <AuthContext.Provider value={{ user, token, login, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
