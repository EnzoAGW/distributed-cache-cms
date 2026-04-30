"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { getContentById, deleteContent, type ContentItem } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";

export default function ArticlePage() {
  const { id } = useParams<{ id: string }>();
  const { token } = useAuth();
  const router = useRouter();
  const [article, setArticle] = useState<ContentItem | null>(null);
  const [error, setError] = useState("");
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    getContentById(id)
      .then(setArticle)
      .catch(() => setError("Artigo não encontrado."));
  }, [id]);

  async function handleDelete() {
    if (!token || !article) return;
    if (!confirm("Tem certeza que quer deletar este artigo?")) return;
    setDeleting(true);
    try {
      await deleteContent(token, article.id);
      router.push("/");
    } catch {
      setError("Erro ao deletar o artigo.");
      setDeleting(false);
    }
  }

  if (error) {
    return (
      <div className="text-center py-16">
        <p className="text-red-600 mb-4">{error}</p>
        <Link href="/" className="text-indigo-600 hover:underline">Voltar</Link>
      </div>
    );
  }

  if (!article) {
    return (
      <div className="space-y-4 animate-pulse">
        <div className="h-7 bg-gray-200 rounded w-2/3" />
        <div className="h-4 bg-gray-100 rounded w-full" />
        <div className="h-4 bg-gray-100 rounded w-5/6" />
        <div className="h-4 bg-gray-100 rounded w-4/5" />
      </div>
    );
  }

  return (
    <div>
      <Link href="/" className="text-sm text-indigo-600 hover:underline mb-4 block">
        ← Voltar para artigos
      </Link>

      <article className="bg-white rounded-xl border border-gray-200 p-6">
        <h1 className="text-2xl font-bold mb-2">{article.title}</h1>

        <div className="flex items-center gap-2 flex-wrap mb-4">
          {article.tags.map((t) => (
            <span key={t} className="bg-indigo-50 text-indigo-700 text-xs px-2 py-0.5 rounded-full">
              {t}
            </span>
          ))}
          <span className="ml-auto text-xs text-gray-400">
            Publicado em {new Date(article.createdAtUtc).toLocaleDateString("pt-BR")}
          </span>
        </div>

        <div className="text-gray-700 leading-relaxed whitespace-pre-wrap border-t border-gray-100 pt-4">
          {article.body}
        </div>
      </article>

      {token && (
        <div className="flex gap-3 mt-4">
          <Link
            href={`/admin?edit=${article.id}`}
            className="bg-indigo-600 hover:bg-indigo-700 text-white text-sm px-4 py-2 rounded-lg transition-colors"
          >
            Editar
          </Link>
          <button
            onClick={handleDelete}
            disabled={deleting}
            className="bg-red-50 hover:bg-red-100 text-red-700 text-sm px-4 py-2 rounded-lg transition-colors disabled:opacity-50"
          >
            {deleting ? "Deletando..." : "Deletar"}
          </button>
        </div>
      )}
    </div>
  );
}
