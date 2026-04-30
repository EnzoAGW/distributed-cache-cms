"use client";

import { createContext, useContext, useEffect, useState } from "react";

interface AuthState {
  token: string | null;
  setToken: (t: string | null) => void;
}

const AuthContext = createContext<AuthState>({ token: null, setToken: () => {} });

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setTokenState] = useState<string | null>(null);

  useEffect(() => {
    setTokenState(localStorage.getItem("cms_token"));
  }, []);

  function setToken(t: string | null) {
    if (t) localStorage.setItem("cms_token", t);
    else localStorage.removeItem("cms_token");
    setTokenState(t);
  }

  return <AuthContext.Provider value={{ token, setToken }}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  return useContext(AuthContext);
}
