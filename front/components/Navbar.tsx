"use client";

import { useState } from "react";
import Link from "next/link";
import { useAuth } from "@/lib/auth-context";
import { useRouter } from "next/navigation";

export default function Navbar() {
  const { token, setToken } = useAuth();
  const router = useRouter();
  const [open, setOpen] = useState(false);

  function logout() {
    setToken(null);
    router.push("/");
    setOpen(false);
  }

  return (
    <header className="bg-white border-b border-gray-200 shadow-sm">
      <div className="max-w-4xl mx-auto px-4 h-14 flex items-center justify-between">
        <Link
          href="/"
          onClick={() => setOpen(false)}
          className="text-lg font-bold text-indigo-600 hover:text-indigo-800"
        >
          CMS Blog
        </Link>

        {/* Desktop nav */}
        <nav className="hidden sm:flex items-center gap-4 text-sm">
          <Link href="/" className="text-gray-600 hover:text-gray-900 transition-colors">
            Artigos
          </Link>
          {token ? (
            <>
              <Link href="/admin" className="text-gray-600 hover:text-gray-900 transition-colors">
                Admin
              </Link>
              <button
                onClick={logout}
                className="bg-gray-100 hover:bg-gray-200 text-gray-700 px-3 py-1.5 rounded-lg transition-colors"
              >
                Sair
              </button>
            </>
          ) : (
            <Link
              href="/login"
              className="bg-indigo-600 hover:bg-indigo-700 text-white px-3 py-1.5 rounded-lg transition-colors"
            >
              Login
            </Link>
          )}
        </nav>

        {/* Hamburger button — mobile only */}
        <button
          className="sm:hidden flex flex-col justify-center items-center w-10 h-10 gap-1.5 rounded-lg hover:bg-gray-100 transition-colors"
          onClick={() => setOpen((v) => !v)}
          aria-label={open ? "Fechar menu" : "Abrir menu"}
          aria-expanded={open}
        >
          <span
            className={`block w-5 h-0.5 bg-gray-700 transition-all duration-200 ${
              open ? "rotate-45 translate-y-2" : ""
            }`}
          />
          <span
            className={`block w-5 h-0.5 bg-gray-700 transition-all duration-200 ${
              open ? "opacity-0 scale-x-0" : ""
            }`}
          />
          <span
            className={`block w-5 h-0.5 bg-gray-700 transition-all duration-200 ${
              open ? "-rotate-45 -translate-y-2" : ""
            }`}
          />
        </button>
      </div>

      {/* Mobile menu dropdown */}
      {open && (
        <div className="sm:hidden border-t border-gray-100 bg-white px-4 py-2">
          <nav className="flex flex-col">
            <Link
              href="/"
              onClick={() => setOpen(false)}
              className="text-gray-700 hover:text-indigo-600 py-3 text-sm border-b border-gray-50 transition-colors"
            >
              Artigos
            </Link>
            {token ? (
              <>
                <Link
                  href="/admin"
                  onClick={() => setOpen(false)}
                  className="text-gray-700 hover:text-indigo-600 py-3 text-sm border-b border-gray-50 transition-colors"
                >
                  Admin
                </Link>
                <button
                  onClick={logout}
                  className="text-left text-red-600 hover:text-red-800 py-3 text-sm transition-colors"
                >
                  Sair
                </button>
              </>
            ) : (
              <Link
                href="/login"
                onClick={() => setOpen(false)}
                className="text-indigo-600 hover:text-indigo-800 py-3 text-sm transition-colors"
              >
                Login
              </Link>
            )}
          </nav>
        </div>
      )}
    </header>
  );
}
