"use client";

export const dynamic = "force-dynamic";

import { Suspense, useEffect, useState, useCallback } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import {
  listContent,
  createContent,
  updateContent,
  deleteContent,
  getContentById,
  type ContentItem,
} from "@/lib/api";
import { useAuth } from "@/lib/auth-context";

const empty = { slug: "", title: "", body: "", tags: "" };

function AdminPageContent() {
  const { token } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const editId = searchParams.get("edit");

  const [articles, setArticles] = useState<ContentItem[]>([]);
  const [form, setForm] = useState(empty);
  const [editing, setEditing] = useState<ContentItem | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const loadArticles = useCallback(async () => {
    try {
      const data = await listContent({ pageSize: 100 });
      setArticles(data.items);
    } catch {
      setError("Erro ao carregar artigos.");
    }
  }, []);

  useEffect(() => {
    if (!token) {
      router.push("/login");
      return;
    }
    loadArticles();
  }, [token, router, loadArticles]);

  useEffect(() => {
    if (!editId || !token) return;
    getContentById(editId).then((item) => {
      setEditing(item);
      setForm({ slug: item.slug, title: item.title, body: item.body, tags: item.tags.join(", ") });
    });
  }, [editId, token]);

  function resetForm() {
    setForm(empty);
    setEditing(null);
    setError("");
    router.push("/admin");
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return;
    setError("");
    setSuccess("");
    setSubmitting(true);

    const tags = form.tags.split(",").map((t) => t.trim()).filter(Boolean);
    try {
      if (editing) {
        await updateContent(token, editing.id, {
          slug: form.slug,
          title: form.title,
          body: form.body,
          tags,
          expectedVersion: editing.version,
        });
        setSuccess("Artigo atualizado!");
      } else {
        await createContent(token, { slug: form.slug, title: form.title, body: form.body, tags });
        setSuccess("Artigo criado!");
      }
      resetForm();
      loadArticles();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao salvar");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDelete(item: ContentItem) {
    if (!token) return;
    if (!confirm(`Deletar "${item.title}"?`)) return;
    try {
      await deleteContent(token, item.id);
      setSuccess("Artigo deletado.");
      loadArticles();
    } catch {
      setError("Erro ao deletar.");
    }
  }

  if (!token) return null;

  return (
    <div className="space-y-8">
      <h1 className="text-xl sm:text-2xl font-bold">Painel Admin</h1>

      {/* Formulário */}
      <section className="bg-white rounded-xl border border-gray-200 p-4 sm:p-6">
        <h2 className="font-semibold text-lg mb-4">{editing ? "Editar artigo" : "Novo artigo"}</h2>

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-3 py-2 text-sm mb-4">
            {error}
          </div>
        )}
        {success && (
          <div className="bg-green-50 border border-green-200 text-green-700 rounded-lg px-3 py-2 text-sm mb-4">
            {success}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Título</label>
              <input
                required
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Slug</label>
              <input
                required
                value={form.slug}
                onChange={(e) => setForm({ ...form, slug: e.target.value })}
                placeholder="meu-artigo"
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400"
              />
            </div>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Tags (separadas por vírgula)</label>
            <input
              value={form.tags}
              onChange={(e) => setForm({ ...form, tags: e.target.value })}
              placeholder="tech, tutorial, news"
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Conteúdo</label>
            <textarea
              required
              rows={6}
              value={form.body}
              onChange={(e) => setForm({ ...form, body: e.target.value })}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-400 resize-y"
            />
          </div>
          <div className="flex flex-wrap gap-3">
            <button
              type="submit"
              disabled={submitting}
              className="bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-medium px-5 py-2 rounded-lg transition-colors disabled:opacity-50"
            >
              {submitting ? "Salvando..." : editing ? "Salvar alterações" : "Publicar"}
            </button>
            {editing && (
              <button
                type="button"
                onClick={resetForm}
                className="bg-gray-100 hover:bg-gray-200 text-gray-700 text-sm px-4 py-2 rounded-lg transition-colors"
              >
                Cancelar
              </button>
            )}
          </div>
        </form>
      </section>

      {/* Lista de artigos */}
      <section>
        <h2 className="font-semibold text-lg mb-3">Artigos publicados ({articles.length})</h2>
        {articles.length === 0 ? (
          <p className="text-gray-500 text-sm">Nenhum artigo ainda.</p>
        ) : (
          <div className="space-y-2">
            {articles.map((item) => (
              <div
                key={item.id}
                className="bg-white rounded-xl border border-gray-200 px-3 sm:px-4 py-3 flex flex-wrap items-center gap-2 sm:gap-3"
              >
                <div className="flex-1 min-w-0">
                  <p className="font-medium text-sm truncate">{item.title}</p>
                  <p className="text-xs text-gray-400 truncate">{item.slug}</p>
                </div>
                <div className="flex gap-1.5 sm:gap-2 shrink-0">
                  <Link
                    href={`/articles/${item.id}`}
                    className="text-xs text-gray-500 hover:text-gray-900 px-2 py-1.5 rounded hover:bg-gray-100 transition-colors"
                  >
                    Ver
                  </Link>
                  <button
                    onClick={() => {
                      setEditing(item);
                      setForm({ slug: item.slug, title: item.title, body: item.body, tags: item.tags.join(", ") });
                      setSuccess("");
                      setError("");
                      window.scrollTo({ top: 0, behavior: "smooth" });
                    }}
                    className="text-xs text-indigo-600 hover:text-indigo-800 px-2 py-1.5 rounded hover:bg-indigo-50 transition-colors"
                  >
                    Editar
                  </button>
                  <button
                    onClick={() => handleDelete(item)}
                    className="text-xs text-red-600 hover:text-red-800 px-2 py-1.5 rounded hover:bg-red-50 transition-colors"
                  >
                    Deletar
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

export default function AdminPage() {
  return (
    <Suspense>
      <AdminPageContent />
    </Suspense>
  );
}
