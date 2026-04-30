"use client";

import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { listContent, type ContentItem, type PagedResult } from "@/lib/api";

export default function Home() {
  const [data, setData] = useState<PagedResult<ContentItem> | null>(null);
  const [search, setSearch] = useState("");
  const [tag, setTag] = useState("");
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const result = await listContent({ page, pageSize: 10, search: search || undefined, tag: tag || undefined });
      setData(result);
    } catch {
      setError("Não foi possível carregar os artigos. Verifique se a API está rodando.");
    } finally {
      setLoading(false);
    }
  }, [page, search, tag]);

  useEffect(() => {
    load();
  }, [load]);

  const totalPages = data ? Math.ceil(data.totalCount / data.pageSize) : 0;

  return (
    <div>
      <h1 className="text-2xl font-bold mb-6">Artigos</h1>

      {/* Filtros */}
      <div className="flex gap-3 mb-6">
        <input
          type="text"
          placeholder="Buscar por título ou conteúdo..."
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          className="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400"
        />
        <input
          type="text"
          placeholder="Filtrar por tag..."
          value={tag}
          onChange={(e) => { setTag(e.target.value); setPage(1); }}
          className="w-40 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400"
        />
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 mb-4 text-sm">
          {error}
        </div>
      )}

      {loading ? (
        <div className="space-y-3">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="bg-white rounded-xl border border-gray-200 p-5 animate-pulse">
              <div className="h-4 bg-gray-200 rounded w-2/3 mb-3" />
              <div className="h-3 bg-gray-100 rounded w-full mb-2" />
              <div className="h-3 bg-gray-100 rounded w-4/5" />
            </div>
          ))}
        </div>
      ) : data?.items.length === 0 ? (
        <p className="text-gray-500 text-center py-16">Nenhum artigo encontrado.</p>
      ) : (
        <div className="space-y-4">
          {data?.items.map((item) => (
            <Link
              key={item.id}
              href={`/articles/${item.id}`}
              className="block bg-white rounded-xl border border-gray-200 p-5 hover:border-indigo-300 hover:shadow-sm transition-all"
            >
              <h2 className="font-semibold text-lg mb-1">{item.title}</h2>
              <p className="text-gray-500 text-sm line-clamp-2 mb-3">{item.body}</p>
              <div className="flex items-center gap-2 flex-wrap">
                {item.tags.map((t) => (
                  <span key={t} className="bg-indigo-50 text-indigo-700 text-xs px-2 py-0.5 rounded-full">
                    {t}
                  </span>
                ))}
                <span className="ml-auto text-xs text-gray-400">
                  {new Date(item.createdAtUtc).toLocaleDateString("pt-BR")}
                </span>
              </div>
            </Link>
          ))}
        </div>
      )}

      {/* Paginação */}
      {totalPages > 1 && (
        <div className="flex justify-center gap-2 mt-8">
          <button
            disabled={page === 1}
            onClick={() => setPage(page - 1)}
            className="px-4 py-2 rounded-lg border border-gray-300 text-sm disabled:opacity-40 hover:bg-gray-100 transition-colors"
          >
            Anterior
          </button>
          <span className="px-4 py-2 text-sm text-gray-600">
            {page} / {totalPages}
          </span>
          <button
            disabled={page === totalPages}
            onClick={() => setPage(page + 1)}
            className="px-4 py-2 rounded-lg border border-gray-300 text-sm disabled:opacity-40 hover:bg-gray-100 transition-colors"
          >
            Próximo
          </button>
        </div>
      )}
    </div>
  );
}
