# Azure Entra ID / Security Specification

## Decision
Use **Microsoft Entra ID** (Azure AD) as the application's OIDC identity provider via the Microsoft identity platform v2.0 endpoints.

Official references:
- https://learn.microsoft.com/en-us/entra/identity-platform/v2-protocols-oidc
- https://learn.microsoft.com/en-us/entra/identity-platform/scenario-web-app-sign-user-sign-in

## Flow
```text
Browser
  |
  | protected page
  v
Blazor Server
  |
  | OIDC challenge
  v
Microsoft Entra ID (login.microsoftonline.com)
  |
  | Authorization Code flow
  v
Blazor Server callback (/signin-oidc)
  |
  | server-side authentication session/cookie
  v
Authenticated Blazor circuit
```

## Required Entra configuration
Create an **App registration** in Microsoft Entra ID:

1. Register a web application (confidential client for server-side Blazor).
2. Add a client secret (store in user secrets / Key Vault — never commit).
3. Enable **Authorization Code** grant (default for web apps).
4. Add redirect URI(s) for each environment:
   - `https://{host}/signin-oidc`
   - Local dev example: `https://localhost:7xxx/signin-oidc`
5. Add front-channel logout URL:
   - `https://{host}/signout-callback-oidc`
6. Optional: assign **App roles** for future RBAC (`roles` claim).

## Configuration
Never commit real secrets.

```json
{
  "Authentication": {
    "Mode": "Entra",
    "Entra": {
      "TenantId": "{tenant-id-or-common}",
      "ClientId": "{application-client-id}",
      "ClientSecret": "",
      "Instance": "https://login.microsoftonline.com/",
      "CallbackPath": "/signin-oidc",
      "SignedOutCallbackPath": "/signout-callback-oidc",
      "SignedOutRedirectUri": "/"
    }
  }
}
```

Use environment variables / user secrets locally and deployment secret storage in hosted environments.

`TenantId` may be a specific tenant GUID, or `organizations` / `common` for multi-tenant scenarios.

## ASP.NET Core implementation
- Cookie authentication for local web session.
- OpenID Connect handler for Entra challenge/callback.
- Authorization Code flow.
- `SaveTokens = false` unless downstream API access is required later.
- Require authenticated users globally (fallback policy).
- `[AllowAnonymous]` on error/not-found pages.

## Claims
Minimum application claims:
- `sub`: immutable external user key used for audit fields.
- `email` / `preferred_username`: display/contact.
- `name`: display name.
- `roles`: optional app roles for future authorization.

`ICurrentUserService` maps `sub` → `UserId` for CreatedBy/UpdatedBy fields.

## Authorization
POC policy: any authenticated user can use business functions.

Policy constants for future RBAC:
- `Policies.AuthenticatedUser`

## Security requirements
- HTTPS required outside local development.
- Anti-forgery protections remain enabled.
- No custom password storage.
- No identity tokens in SQL.
- No secrets in source control.
- Validate all server-side commands even if UI validates.
- All Dapper SQL is parameterized.
- Log no passwords/tokens/secrets.
