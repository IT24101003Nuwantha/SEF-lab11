# CampusEats - Integrated Full-Stack Application (SE3090 Lab 11)

## Project Overview

CampusEats is a full-stack campus food ordering platform created for SE3090. This repository integrates:
- **Frontend (Lab 02):** React (v19) Single Page Application built with Vite, React Router, and responsive UI components.
- **Backend (Lab 04):** ASP.NET Core Web API targeting .NET 8, featuring Entity Framework Core with PostgreSQL / Supabase, JWT-based authentication, BCrypt password hashing, native `.env` configuration support, and Swagger/OpenAPI documentation.

---

## Repository Structure

```text
lab11/
├── frontend/
│   ├── src/
│   │   ├── config/
│   │   │   └── api.js              # Centralized API base URL configuration
│   │   ├── features/
│   │   │   └── menu/
│   │   │       ├── components/     # MenuList, DishCard
│   │   │       ├── hooks/          # useFetch, useDebounce
│   │   │       └── pages/          # MenuPage, DishDetailPage, OrderPage
│   │   ├── App.jsx
│   │   └── main.jsx
│   ├── public/
│   ├── package.json
│   ├── vite.config.js
│   └── .env.example                # Frontend environment template
│
├── backend/
│   ├── Controllers/                # MenuController, AuthController
│   ├── Data/                       # AppDbContext
│   ├── Dtos/                       # MenuItemDto, AuthDtos, CreateMenuItemDto
│   ├── Migrations/                 # EF Core migrations
│   ├── Models/                     # MenuItem, User
│   ├── Services/                   # MenuService, IMenuService
│   ├── appsettings.json            # Base application settings (placeholders)
│   ├── appsettings.Development.json# Local development settings
│   ├── CampusEats.Api.csproj       # .NET 8 Web API project file
│   ├── CampusEats.Api.sln          # Solution file
│   ├── Dockerfile                  # Multi-stage production container build
│   ├── .dockerignore
│   ├── .env.example                # Backend environment template
│   └── .env                        # Local development variables (git-ignored)
│
├── .gitignore                      # Root Git ignore rules
├── .env.example                    # Root environment variable template
└── README.md                       # Integration and run documentation
```

---

## Local Development

### Prerequisites
- Node.js (v18+) and npm
- .NET 8 SDK
- PostgreSQL database (Local Docker container OR Supabase cloud database)

### 1. Database Setup

#### Option A: Supabase Cloud Database (Recommended)
1. Create a project at [supabase.com](https://supabase.com).
2. Go to **Project Settings** → **Database** → **Connection string**.
3. Copy either the **URI** format or **ADO.NET** key-value format (use port `5432` Session pooler / Direct mode for schema migrations).
4. In `backend/.env`, set:
   ```env
   ConnectionStrings__Default=postgresql://postgres.[YOUR-PROJECT-REF]:[YOUR-PASSWORD]@aws-0-[REGION].pooler.supabase.com:5432/postgres
   ```
   *(The backend automatically parses both URI and ADO.NET formats and enforces required SSL mode).*
5. On startup, EF Core automatically applies all migrations and seeds initial menu data into your Supabase database.

#### Option B: Local Docker PostgreSQL
```bash
docker run --name pg -e POSTGRES_PASSWORD=MyStrongPass123 -p 5433:5432 -d postgres:16
docker exec -it pg psql -U postgres -c "CREATE DATABASE campuseats;"
```

### 2. Backend Setup
```bash
cd backend
dotnet restore
dotnet run
```
The backend supports `.env` files via `dotenv.net`. It starts on `http://localhost:5178`. Swagger UI is available at `http://localhost:5178/swagger`.

### 3. Frontend Setup
```bash
cd frontend
npm install
npm run dev
```
The Vite development server runs at `http://localhost:5173`.

---

## Required Environment Variables

### Frontend (`frontend/.env`)
| Variable | Description | Local Default |
| :--- | :--- | :--- |
| `VITE_API_URL` | Base URL pointing to the backend API | `http://localhost:5178` |

### Backend (`backend/.env` or Host Environment)
The backend loads variables directly from `backend/.env` (or root `.env`), as well as container environment variables:
| Variable | Description | Example / Default |
| :--- | :--- | :--- |
| `ConnectionStrings__Default` (or `DATABASE_URL`) | PostgreSQL / Supabase connection string | Supabase URI or local `Host=localhost;Port=5433;Database=campuseats;...` |
| `Jwt__Key` | Secret key for signing JWT tokens (min 32 characters) | `dev-only-secret-change-me-min-32-characters!!` |
| `Jwt__Issuer` | JWT issuer string | `CampusEats` |
| `Jwt__Audience` | JWT audience string | `CampusEatsUsers` |
| `Frontend__Url` (or `FRONTEND_URL`) | Allowed origin for CORS | `http://localhost:5173` |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment mode | `Development` or `Production` |
| `PORT` | Container listening port (when deployed) | `8080` |

---

## Production Deployment Plan

### Frontend
- **Hosting Platform:** Vercel (or Netlify / Cloudflare Pages)
- **Root Directory:** `frontend`
- **Build Command:** `npm run build`
- **Output Directory:** `dist`
- **Environment Variables:**
  - `VITE_API_URL`: URL of the deployed backend API (e.g., `https://campuseats-api.onrender.com`)

### Backend
- **Hosting Platform:** Render or Railway
- **Root / Docker Context:** `backend`
- **Build Type:** Dockerfile (`backend/Dockerfile`)
- **Port:** `8080`
- **Environment Variables:**
  - `ConnectionStrings__Default` (or `DATABASE_URL`): Supabase connection string
  - `Jwt__Key`: Production secret key (64+ character random string)
  - `Jwt__Issuer`: `CampusEats`
  - `Jwt__Audience`: `CampusEatsUsers`
  - `Frontend__Url`: URL of the deployed frontend on Vercel
  - `ASPNETCORE_ENVIRONMENT`: `Production`

### Database
- **Provider:** Supabase Managed PostgreSQL
- EF Core database migrations are automatically applied on application startup via `db.Database.Migrate()`.

---

## API Integration Summary

| Frontend Feature | Component / Page | HTTP Method | Backend Route | Status | Notes |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Menu Listing & Search** | `MenuPage.jsx` | `GET` | `/api/menu` | **Connected** | Fetches live menu items from database via backend API. Supports real-time debounced search. |
| **Dish Details** | `DishDetailPage.jsx` | `GET` | `/api/menu/{id}` | **Connected** | Fetches and displays individual dish details by ID. |
| **Authentication** | — | `POST` | `/api/auth/register` | Available on Backend | Backend supports user registration with BCrypt hashing. Frontend UI from Lab 02 does not have auth pages. |
| **Authentication** | — | `POST` | `/api/auth/login` | Available on Backend | Backend issues signed JWT tokens. Frontend UI from Lab 02 does not have login pages. |
| **Menu Management** | — | `POST`, `PUT`, `DELETE` | `/api/menu` | Available on Backend | Protected by `[Authorize(Roles = "Admin")]`. |
| **Orders** | `OrderPage.jsx` | — | — | Client-side only | Lab 02 includes a client-side order form; Lab 04 backend did not define an Order entity or controller. |
