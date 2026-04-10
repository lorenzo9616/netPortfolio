---
name: OpenOCR Auth — Simple Login Page
description: Replace the broken API key system with a username/password login page backed by JWT, with default credentials for local use
type: project
---

# OpenOCR — Auth Design

**Date:** 2026-04-10  
**Status:** Approved  
**Audience:** All users — junior devs configure it, marketing testers use it

---

## Problem

The current API key system is broken: the frontend never sends the `X-Api-Key` header, so setting a key in `.env` silently breaks all API calls. Marketing testers need a real login experience. Junior devs need something they can configure without touching code.

---

## Goals

- Testers open the app, see a login page, enter credentials, and never think about auth again.
- Credentials are configurable via `.env` — no code changes needed to change the password.
- Default credentials are pre-set so the app works out-of-the-box for local use.
- All protected routes redirect to `/login` if not authenticated.

---

## Architecture

Single admin user. Credentials stored in `.env`. Backend issues JWTs; frontend stores the token in a cookie set by a Next.js API route (not directly accessible to JavaScript). All `/api/*` backend routes require a valid JWT. Public routes: `/auth/login`, `/health`, `/swagger`.

---

## Components

### 1. `.env.example` — New Variables

```env
AUTH_USERNAME=admin
AUTH_PASSWORD=openocr2024
AUTH_JWT_SECRET=change-me-in-production
AUTH_JWT_EXPIRY_HOURS=24
```

The setup script prints these defaults at the end of setup so testers know what to type without hunting through files.

### 2. Backend — Remove `ApiKeyMiddleware`

Delete `backend/Middleware/ApiKeyMiddleware.cs` and its registration in `Program.cs`.

### 3. Backend — JWT Auth Middleware

Add `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet package. Configure in `Program.cs`:

- Validate tokens signed with `AUTH_JWT_SECRET`.
- Apply `[Authorize]` globally via a default policy — all controllers require auth automatically.
- Exempt `/auth/login`, `/health`, and `/swagger` from the policy.

### 4. Backend — `AuthController`

New controller at `backend/Controllers/AuthController.cs`:

**`POST /auth/login`**
- Accepts `{ username: string, password: string }`.
- Compares against `AUTH_USERNAME` and `AUTH_PASSWORD` from configuration.
- On match: returns `{ token: string, expiresAt: string }` — a signed JWT valid for `AUTH_JWT_EXPIRY_HOURS`.
- On mismatch: returns `401` with `{ message: "Invalid credentials" }`.
- Rate-limited to 5 attempts per minute per IP to prevent brute force (uses existing rate limiter infrastructure).

**`POST /auth/logout`**
- Returns `200` — no server-side state to clear (client discards the token).

### 5. Frontend — Next.js API Route (`/api/auth/login`)

A thin server-side route that:
1. Forwards the `{ username, password }` POST to the backend `/auth/login`.
2. On success, sets an `httpOnly; SameSite=Strict` cookie named `ocr_token` containing the JWT.
3. Returns `200` to the client.

This keeps the JWT out of JavaScript — the browser sends it automatically as a cookie on every request.

### 6. Frontend — `api.ts` Updates

All fetch calls gain `credentials: 'include'` so the cookie is sent automatically. No manual token management needed.

### 7. Frontend — `/login` Page

Clean centered card layout matching the existing Tailwind design system:

- Username and password fields with labels.
- "Sign in" button — shows a spinner while the request is in flight.
- Error banner if credentials are wrong: "Incorrect username or password."
- No "Forgot password" link (local-only phase).

### 8. Frontend — `middleware.ts`

Next.js middleware that runs on every request:

- If the `ocr_token` cookie is missing or expired → redirect to `/login`.
- If the user is on `/login` and already authenticated → redirect to `/properties`.
- Public paths (none beyond `/login` in this phase).

### 9. Frontend — Sidebar Sign Out

"Sign out" button at the bottom of `Sidebar.tsx`:

- Calls `POST /api/auth/logout` (Next.js route) which clears the `ocr_token` cookie.
- Redirects to `/login`.

---

## Data Flow

```
User → /login page → POST /api/auth/login (Next.js API route)
  → POST /auth/login (backend) → validate creds → return JWT
  → Next.js sets httpOnly cookie → redirect to /properties

Subsequent requests:
  Browser sends cookie automatically → Next.js middleware validates presence
  → fetch to backend includes credentials → backend JWT middleware validates token
```

---

## Error Handling

- Wrong credentials: `401` from backend → friendly error message on login page.
- Expired token: backend returns `401` → Next.js middleware catches the redirect and sends user to `/login`.
- Missing `AUTH_JWT_SECRET` in `.env`: backend fails to start with a clear error logged — prevents running with an empty signing key.

---

## Testing

- Unit test: `AuthController` with correct and incorrect credentials (mocked config).
- Integration test: full login flow via `TestServer` — verify token is returned.
- Frontend component test: login form shows error on bad credentials, redirects on success.
- E2E test (Playwright): login → see Properties page → sign out → redirected to login (see Testing spec).

---

## Out of Scope

- Multi-user support (future phase — would require a users table).
- Password reset flow.
- OAuth / SSO integration.
- Role-based access control.
