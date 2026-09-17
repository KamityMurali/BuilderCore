# AWS Cognito / Security Specification

## Decision
Use an **Amazon Cognito User Pool** as the application's OIDC identity provider. Use Cognito managed login for the POC. AWS documents User Pools as OIDC identity providers and managed login as the low-effort browser-based authentication option.

Official references:
- https://docs.aws.amazon.com/cognito/latest/developerguide/cognito-user-pools.html
- https://docs.aws.amazon.com/cognito/latest/developerguide/cognito-integrate-apps.html
- https://docs.aws.amazon.com/cognito/latest/developerguide/cognito-userpools-server-contract-reference.html

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
Cognito Managed Login
  |
  | Authorization Code flow
  v
Blazor Server callback
  |
  | server-side authentication session/cookie
  v
Authenticated Blazor circuit
```

## Required Cognito configuration
Create:
1. User Pool.
2. User Pool domain.
3. Web application/app client suitable for a server-side web app.
4. Authorization Code grant.
5. OIDC scopes: `openid`, `email`, `profile`.
6. Callback URL for each environment.
7. Sign-out URL for each environment.

Do not enable implicit flow.

## Configuration
Never commit real secrets.

```json
{
  "Authentication": {
    "Cognito": {
      "Authority": "https://cognito-idp.{region}.amazonaws.com/{userPoolId}",
      "ClientId": "",
      "ClientSecret": "",
      "MetadataAddress": "",
      "SignedOutRedirectUri": "/"
    }
  }
}
```

Use environment variables / user secrets locally and deployment secret storage in hosted environments.

## ASP.NET Core implementation intent
Use:
- Cookie authentication for local web session.
- OpenID Connect handler for Cognito challenge/callback.
- Authorization Code flow.
- `SaveTokens = false` unless a later requirement explicitly needs downstream tokens.
- Require authenticated users globally/fallback policy.
- Allow anonymous only for explicit login/error endpoints as required.

Exact endpoint/metadata configuration must follow the current Cognito discovery document for the configured User Pool/domain rather than hard-coded guesses.

## Claims
Minimum application claims:
- `sub`: immutable external user key used for audit fields.
- `email`: display/contact.
- `name`, `given_name`, `family_name`: optional display.

`ICurrentUserService`:
```csharp
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    string UserId { get; }     // Cognito sub
    string? Email { get; }
    string DisplayName { get; }
}
```

## Authorization
POC policy: any authenticated user can use business functions.

Still create policy constants so later RBAC does not require rewriting components:
- `Policies.AuthenticatedUser`

Future groups can map to:
- Estimator
- Purchasing
- ProjectCost
- Administrator

Do not implement those roles unless requested.

## Security requirements
- HTTPS required outside local development.
- Anti-forgery protections remain enabled.
- No custom password storage.
- No Cognito tokens in SQL.
- No secrets in source control.
- Validate all server-side commands even if UI validates.
- All Dapper SQL is parameterized; never concatenate user-controlled values into SQL.
- Encode UI output; do not render untrusted HTML.
- Log no passwords/tokens/secrets.
- Use least-privilege SQL credentials.
