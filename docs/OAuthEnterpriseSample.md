# Enterprise OAuth/OIDC Login Sample (.NET 8 API + React SPA)

This sample shows a **Big-4 style baseline** for OAuth/OIDC in enterprise apps:

- **Authorization Code + PKCE** for SPA login.
- **OpenID Connect** for identity (`openid`, `profile`, `email`).
- **OAuth 2.0 access token** for API authorization.
- **JWT bearer validation** in .NET API.
- **Role/group-based authorization** policies.

> Identity provider can be Microsoft Entra ID, Okta, Auth0, Keycloak, or Ping.

---

## 1) High-level flow

1. React app redirects user to IdP login.
2. IdP returns authorization code to SPA callback URL.
3. SPA exchanges code (with PKCE verifier) for tokens.
4. SPA stores access token in memory/session-managed OIDC client.
5. React calls .NET API with `Authorization: Bearer <access_token>`.
6. .NET API validates JWT using issuer + audience + signing keys.
7. .NET API enforces policy-based authorization (roles/scopes).

---

## 2) .NET 8 API setup (JWT + policy authorization)

### `src/InvoiceBilling.Api/Program.cs` (sample)

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"]; // e.g. https://login.microsoftonline.com/{tenantId}/v2.0
        options.Audience = builder.Configuration["Auth:Audience"];   // API App Registration / resource
        options.RequireHttpsMetadata = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Invoice.Read", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim("scp", "invoice.read") ||
            ctx.User.IsInRole("InvoiceReader")));

    options.AddPolicy("Invoice.Write", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim("scp", "invoice.write") ||
            ctx.User.IsInRole("InvoiceAdmin")));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy
            .WithOrigins(builder.Configuration["App:FrontendUrl"]!)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
```

### Protected controller example

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/invoices")]
public class InvoicesController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Invoice.Read")]
    public IActionResult GetInvoices() => Ok(new[] { "INV-1001", "INV-1002" });

    [HttpPost]
    [Authorize(Policy = "Invoice.Write")]
    public IActionResult CreateInvoice([FromBody] object payload) => Ok(new { status = "created" });
}
```

### `appsettings.json` sample

```json
{
  "Auth": {
    "Authority": "https://your-idp.example.com/realms/finance",
    "Audience": "invoice-api"
  },
  "App": {
    "FrontendUrl": "https://localhost:5173"
  }
}
```

---

## 3) React setup with `oidc-client-ts`

Install:

```bash
npm i oidc-client-ts axios
```

### `src/auth/authConfig.ts`

```ts
import { UserManager, WebStorageStateStore } from "oidc-client-ts";

export const oidcManager = new UserManager({
  authority: import.meta.env.VITE_OIDC_AUTHORITY,
  client_id: import.meta.env.VITE_OIDC_CLIENT_ID,
  redirect_uri: `${window.location.origin}/auth/callback`,
  post_logout_redirect_uri: `${window.location.origin}/`,
  response_type: "code",
  scope: "openid profile email invoice.read invoice.write",
  automaticSilentRenew: true,
  monitorSession: true,
  userStore: new WebStorageStateStore({ store: window.sessionStorage })
});
```

### `src/auth/AuthProvider.tsx`

```tsx
import React, { createContext, useContext, useEffect, useMemo, useState } from "react";
import { oidcManager } from "./authConfig";

type AuthContextValue = {
  isAuthenticated: boolean;
  accessToken?: string;
  login: () => Promise<void>;
  logout: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export const AuthProvider: React.FC<React.PropsWithChildren> = ({ children }) => {
  const [accessToken, setAccessToken] = useState<string | undefined>();

  useEffect(() => {
    oidcManager.getUser().then(user => {
      if (user && !user.expired) setAccessToken(user.access_token);
    });
  }, []);

  const value = useMemo(() => ({
    isAuthenticated: !!accessToken,
    accessToken,
    login: async () => oidcManager.signinRedirect(),
    logout: async () => oidcManager.signoutRedirect()
  }), [accessToken]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used inside AuthProvider");
  return ctx;
};
```

### `src/auth/AuthCallback.tsx`

```tsx
import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { oidcManager } from "./authConfig";

export default function AuthCallback() {
  const navigate = useNavigate();

  useEffect(() => {
    oidcManager.signinRedirectCallback()
      .then(() => navigate("/"))
      .catch(() => navigate("/login?error=callback_failed"));
  }, [navigate]);

  return <p>Signing you in...</p>;
}
```

### API client with token attachment

```ts
import axios from "axios";
import { oidcManager } from "./auth/authConfig";

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL
});

api.interceptors.request.use(async config => {
  const user = await oidcManager.getUser();
  if (user?.access_token) {
    config.headers.Authorization = `Bearer ${user.access_token}`;
  }
  return config;
});
```

---

## 4) Enterprise hardening checklist

- Use **BFF pattern** for very high-security apps (tokens remain server-side).
- Keep token lifetime short (5-15 mins) + refresh token rotation.
- Never store tokens in `localStorage` for sensitive workloads.
- Enforce HTTPS, HSTS, secure cookies, strict CORS, and CSP.
- Map groups/roles from IdP claims to app policies.
- Centralize audit logging (login success/failure, consent, privilege changes).
- Add defense controls: rate limiting, anomaly detection, MFA/Conditional Access.

---

## 5) Typical enterprise app registration settings

In your IdP, configure:

- SPA client:
  - Redirect URI: `https://localhost:5173/auth/callback`
  - Grant: Authorization Code + PKCE
  - Scopes: `openid profile email invoice.read invoice.write`
- API resource:
  - Audience: `invoice-api`
  - Exposed scopes: `invoice.read`, `invoice.write`
- Optional roles:
  - `InvoiceReader`, `InvoiceAdmin`

This gives you a maintainable baseline aligned with common enterprise IAM standards.
