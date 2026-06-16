# Headless CMS

Full-stack content management system for publishing and managing articles. Public read access with paginated search and tag filtering — authenticated write access for admins.

## Stack

| Layer | Technology |
|---|---|
| Frontend | Next.js 16 · React 19 · TypeScript · Tailwind CSS 4 |
| Backend | ASP.NET Core 10 · C# |
| Database | PostgreSQL 17 |
| Cache | Redis 7 (decorator pattern, falls back to in-memory) |
| Auth | JWT Bearer · rate-limited (10 req/min) |
| Tests | xUnit · Moq · WebApplicationFactory |

## Features

- CRUD for content items (articles) with slug, title, body, and tags
- Full-text search and tag filtering with pagination
- Distributed caching with Redis — list cache (45s TTL), item cache (300s TTL)
- HTTP-level caching — ETag, `Cache-Control`, 304 Not Modified
- Optimistic concurrency control — 409 Conflict on version mismatch
- Health check endpoint
- Structured logging

## Project Structure

```
WebApplication2/
├── back/                    # ASP.NET Core API
│   ├── Controllers/         # ContentController + AuthController
│   ├── Services/            # ContentService (business logic)
│   ├── Repositories/        # EF Core + InMemory + Cached (decorator)
│   ├── Persistence/         # AppDbContext + Migrations
│   ├── Models/              # ContentItem, PagedResult, queries
│   ├── Dtos/                # Request/response DTOs
│   ├── Options/             # Typed config (JWT, Admin, Cache)
│   └── HealthChecks/        # CacheHealthCheck
├── front/                   # Next.js (App Router)
│   └── app/
│       ├── page.tsx         # Public article listing
│       ├── articles/[id]/   # Article detail
│       ├── admin/           # Protected admin panel
│       └── login/           # Auth page
├── project.Tests/           # xUnit unit + integration tests
├── docker-compose.yml       # PostgreSQL + Redis + API + Frontend
└── .env.example             # Environment variables template
```

## Running locally

### Option 1 — Docker (recommended)

```bash
cp .env.example .env
docker compose up
```

| Service | URL |
|---|---|
| Frontend | http://localhost:3000 |
| API | http://localhost:8080 |
| Swagger | http://localhost:8080/swagger |

### Option 2 — Manual

**Prerequisites:** .NET 10 SDK · Node 20+ · PostgreSQL · Redis

**1. Backend**

```bash
cd back
# Configure connection string in appsettings.Development.json
dotnet run
# API at http://localhost:5000
```

**2. Frontend**

```bash
cd front
npm install
npm run dev
# App at http://localhost:3000
```

## Environment variables

See `.env.example` for the full list. Key variables:

```env
POSTGRES_PASSWORD=your_password
JWT_KEY=your_secret_key_min_32_chars
ADMIN_USERNAME=admin
ADMIN_PASSWORD=your_admin_password
NEXT_PUBLIC_API_URL=http://localhost:8080
```

## API overview

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/token` | No | Get JWT token (rate limited) |
| GET | `/api/content` | No | List articles (paginated, filterable) |
| GET | `/api/content/{id}` | No | Get by ID (ETag support) |
| GET | `/api/content/slug/{slug}` | No | Get by slug |
| POST | `/api/content` | Yes | Create article |
| PUT | `/api/content/{id}` | Yes | Update (optimistic concurrency) |
| DELETE | `/api/content/{id}` | Yes | Delete |
| GET | `/health` | No | Health check |

**List query params:** `?page=1&pageSize=10&tag=docker&search=containers`

**Update requires version field** (prevents lost writes):
```json
{ "title": "...", "body": "...", "expectedVersion": 3 }
```

Returns `409 Conflict` if version does not match current record.

## Caching architecture

```
GET /api/content/{id}
  → CachedContentRepository checks Redis
    → HIT: return cached item (300s TTL)
    → MISS: query PostgreSQL, write to Redis, return item

POST / PUT / DELETE
  → Increments global generation counter
  → All list cache keys are invalidated automatically
```

## Tests

```bash
cd project.Tests
dotnet test
```

- **Unit** — `ContentService` business logic (14 tests), cache decorator behavior
- **Integration** — Auth and Content endpoints via `WebApplicationFactory`
