# Content Management API

A lightweight, high-performance headless CMS REST API built with **ASP.NET Core 10** and **C#**. Designed as a portfolio project to demonstrate clean architecture, distributed caching, JWT authentication, and testable service design.

---

## Features

- Full CRUD for content items (articles, posts, pages)
- Tag-based filtering and full-text search with pagination
- **Distributed caching** with Redis (falls back to in-memory) — cache-aside pattern with generation-based list invalidation
- **HTTP caching** — `ETag`, `Cache-Control`, and `304 Not Modified` support
- **JWT authentication** — read endpoints are public; write operations require a Bearer token
- **Optimistic concurrency control** — optional `ExpectedVersion` field on updates prevents lost writes
- **Global exception handling** — domain exceptions map to standardized `ProblemDetails` responses
- **Structured logging** via `ILogger` throughout the service layer
- **14 unit tests** covering all service operations, cache behavior, and edge cases

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 10 |
| Language | C# 13 |
| Caching | Redis (optional) / In-Memory |
| Authentication | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) |
| API Docs | OpenAPI (Swagger) |
| Testing | xUnit + Moq |

---

## Project Structure

```
WebApplication2/
├── Controllers/
│   ├── AuthController.cs        # POST /api/auth/token
│   └── ContentController.cs     # CRUD endpoints
├── Dtos/                        # Request / response contracts
├── Exceptions/                  # Domain exceptions (DuplicateSlug, ConcurrencyConflict)
├── Models/                      # Domain entities and query/result records
├── Options/                     # Typed configuration (ContentCacheOptions, JwtOptions)
├── Repositories/
│   ├── IContentRepository.cs
│   └── InMemoryContentRepository.cs
├── Services/
│   ├── IContentService.cs
│   └── ContentService.cs        # Business logic + caching layer
├── Program.cs
├── appsettings.json
└── appsettings.Development.json

WebApplication2.Tests/
└── Unit/
    └── ContentServiceTests.cs   # 14 unit tests
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- (Optional) A running Redis instance

### Run locally

```bash
git clone <repository-url>
cd WebApplication2
dotnet run
```

The API will be available at `https://localhost:7007` (HTTPS) or `http://localhost:5175` (HTTP).

Swagger UI is available at `https://localhost:7007/openapi/v1.json` in Development mode.

### Run tests

```bash
cd WebApplication2.Tests
dotnet test
```

---

## Configuration

All settings live in `appsettings.json` / `appsettings.Development.json`.

```json
{
  "ConnectionStrings": {
    "Redis": ""
  },
  "ContentCache": {
    "ItemTtlSeconds": 300,
    "ListTtlSeconds": 45
  },
  "Jwt": {
    "Key": "REPLACE_WITH_A_STRONG_SECRET_KEY_AT_LEAST_32_CHARS",
    "Issuer": "WebApplication2",
    "Audience": "WebApplication2",
    "ExpirationMinutes": 60
  }
}
```

- Leave `Redis` empty to use the in-memory distributed cache (no external dependency needed for development).
- Replace the `Jwt.Key` with a secure secret of at least 32 characters before deploying.

---

## API Reference

### Authentication

Obtain a token before calling write endpoints:

```http
POST /api/auth/token
Content-Type: application/json

{
  "username": "admin",
  "password": "admin123"
}
```

**Response:**
```json
{
  "token": "<jwt>",
  "expiresAt": "2026-03-24T23:00:00Z"
}
```

> **Note:** The credentials above are hardcoded for demo purposes. Replace with a real user store before going to production.

Use the token in subsequent requests:

```http
Authorization: Bearer <jwt>
```

---

### Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/content` | — | List content (paginated, filterable) |
| `GET` | `/api/content/{id}` | — | Get by ID |
| `GET` | `/api/content/slug/{slug}` | — | Get by slug |
| `POST` | `/api/content` | Required | Create content |
| `PUT` | `/api/content/{id}` | Required | Update content |
| `DELETE` | `/api/content/{id}` | Required | Delete content |
| `POST` | `/api/auth/token` | — | Obtain JWT token |

---

### List content

```http
GET /api/content?page=1&pageSize=20&tag=dotnet&search=cache
```

**Query parameters:**

| Parameter | Type | Default | Description |
|---|---|---|---|
| `page` | int | 1 | Page number |
| `pageSize` | int | 20 | Items per page (max 100) |
| `tag` | string | — | Filter by tag (case-insensitive) |
| `search` | string | — | Full-text search on title and body |

---

### Create content

```http
POST /api/content
Authorization: Bearer <token>
Content-Type: application/json

{
  "slug": "my-first-post",
  "title": "My First Post",
  "body": "Full content goes here...",
  "tags": ["dotnet", "caching"]
}
```

**Slug format:** lowercase letters, numbers, and hyphens only (e.g. `my-article-2024`).

---

### Update content (with optimistic concurrency)

```http
PUT /api/content/{id}
Authorization: Bearer <token>
Content-Type: application/json

{
  "slug": "my-first-post",
  "title": "Updated Title",
  "body": "Updated content...",
  "tags": ["dotnet"],
  "expectedVersion": 1
}
```

`expectedVersion` is optional. When provided, the server rejects the update with `409 Conflict` if the stored version no longer matches — this prevents two concurrent editors from overwriting each other's changes.

---

### Error responses

All errors return a standard `ProblemDetails` body:

```json
{
  "title": "Duplicate slug",
  "detail": "A content item with slug 'my-post' already exists.",
  "status": 409
}
```

| Status | Cause |
|---|---|
| `400` | Validation failure (invalid slug format, missing required fields) |
| `401` | Missing or invalid JWT token |
| `404` | Content item not found |
| `409` | Duplicate slug or optimistic concurrency conflict |
| `500` | Unexpected server error |

---

## Architecture

The application follows a **layered architecture**:

```
HTTP Request
    │
    ▼
Controller          — maps HTTP ↔ DTOs, handles auth
    │
    ▼
ContentService      — business logic, caching, normalization
    │
    ▼
IContentRepository  — data access abstraction
    │
    ▼
InMemoryContentRepository  — thread-safe in-memory store
                            (swap for EF Core + SQL/PostgreSQL)
```

### Caching strategy

- **Item lookups** (`GET` by ID or slug): cache-aside with a configurable TTL.
- **List queries**: keyed by a global generation counter + SHA-256 hash of the query parameters. Any mutation (create, update, delete) increments the generation, which effectively invalidates all cached lists without needing to track individual keys.
- Both Redis and the built-in in-memory distributed cache are supported via the same `IDistributedCache` abstraction.

---

## Roadmap

- [ ] Persist data with Entity Framework Core + PostgreSQL
- [ ] Integration tests using `WebApplicationFactory<Program>`
- [ ] Role-based authorization (editor vs. admin)
- [ ] Soft deletes with audit trail
- [ ] Rate limiting (`AddRateLimiter`)
- [ ] Docker + docker-compose with Redis

---

## License

MIT
