const BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5175";

export interface ContentItem {
  id: string;
  slug: string;
  title: string;
  body: string;
  tags: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
  version: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface TokenResponse {
  token: string;
  expiresAt: string;
}

function authHeader(token: string) {
  return { Authorization: `Bearer ${token}` };
}

export async function login(username: string, password: string): Promise<TokenResponse> {
  const res = await fetch(`${BASE}/api/auth/token`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username, password }),
  });
  if (!res.ok) throw new Error("Credenciais inválidas");
  return res.json();
}

export async function listContent(params: {
  page?: number;
  pageSize?: number;
  tag?: string;
  search?: string;
}): Promise<PagedResult<ContentItem>> {
  const q = new URLSearchParams();
  if (params.page) q.set("page", String(params.page));
  if (params.pageSize) q.set("pageSize", String(params.pageSize));
  if (params.tag) q.set("tag", params.tag);
  if (params.search) q.set("search", params.search);
  const res = await fetch(`${BASE}/api/content?${q}`, { cache: "no-store" });
  if (!res.ok) throw new Error("Erro ao buscar conteúdo");
  return res.json();
}

export async function getContentById(id: string): Promise<ContentItem> {
  const res = await fetch(`${BASE}/api/content/${id}`, { cache: "no-store" });
  if (!res.ok) throw new Error("Conteúdo não encontrado");
  return res.json();
}

export async function createContent(
  token: string,
  data: { slug: string; title: string; body: string; tags: string[] }
): Promise<ContentItem> {
  const res = await fetch(`${BASE}/api/content`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeader(token) },
    body: JSON.stringify(data),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error((err as { detail?: string }).detail ?? "Erro ao criar conteúdo");
  }
  return res.json();
}

export async function updateContent(
  token: string,
  id: string,
  data: { slug: string; title: string; body: string; tags: string[]; expectedVersion: number }
): Promise<ContentItem> {
  const res = await fetch(`${BASE}/api/content/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...authHeader(token) },
    body: JSON.stringify(data),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error((err as { detail?: string }).detail ?? "Erro ao atualizar conteúdo");
  }
  return res.json();
}

export async function deleteContent(token: string, id: string): Promise<void> {
  const res = await fetch(`${BASE}/api/content/${id}`, {
    method: "DELETE",
    headers: authHeader(token),
  });
  if (!res.ok) throw new Error("Erro ao deletar conteúdo");
}
