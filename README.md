# Punchout Module

## Overview

The Punchout module connects Virto Commerce to e-procurement systems such as Coupa, SAP Ariba, and Oracle over the cXML PunchOut protocol. A buyer starts shopping from inside their procurement system and is redirected to the Virto Commerce storefront, already signed in and working in the context of their organization. The module validates incoming `PunchOutSetupRequest` documents against shared-secret configurations, maps the procurement-system identity to a platform user, opens a time-limited punchout session, and returns a one-time start page URL. The storefront exchanges that URL for an access token through a dedicated `punchout` OAuth grant.

## Key Features

* **cXML PunchOut endpoint**: a single anonymous endpoint (`POST api/punchout/cxml`) accepts cXML documents and always answers with a cXML response. The outcome is carried in the `Status` element, as the protocol requires.
* **PunchOutSetupRequest support**: parses `Header/From`, `Header/To`, `Header/Sender`, `BuyerCookie`, `BrowserFormPost`, `Contact`, and `Extrinsic` elements. Request types without a registered handler return cXML status `450 Not Implemented`.
* **PunchOutOrderMessage support**: comming soon.
* **PunchOutOrderMessage support**: comming soon.
* **OrderRequest and OrderResponse support**: comming soon.
* **Shared-secret authentication**: the sender's `SharedSecret` is compared against every configured integration in constant time, with an optional check of the sender domain.
* **Return URL allow list**: restricts the `BrowserFormPost` URL to exact URLs or `*`-suffixed prefixes for each integration.
* **User mapping**: links an external identity from `Header/Sender/Credential/Identity` to a platform security account. Mappings are managed from the contact details blade in the admin.
* **One-time session tokens**: each setup request issues a cryptographically random 32-byte token. The database stores only its SHA-256 hash, and the token is redeemed atomically, exactly once, before its lifetime ends (15 minutes by default).
* **`punchout` OAuth grant type**: `connect/token` with `grant_type=punchout` redeems the session token and issues an access token for the mapped user. The token is valid until the session expires (4 hours by default), carries `channelId` and `channelSessionId` claims, and comes without a refresh token.
* **Organization scoping**: when a request carries a punchout principal, the buyer's available organizations are narrowed to the current organization through the Profile Experience API pipeline.
* **Extensibility**: new cXML request types can be added as handlers (`ICxmlRequestHandler`), a business hook (`IPunchoutHandler`) can modify or reject a session before it is saved, and models are created through `AbstractTypeFactory`.
* **Multi-database support**: SQL Server, MySQL, and PostgreSQL through dedicated EF Core provider assemblies.

## Configuration

### Punchout Integrations

Integrations are defined in the `Punchout` section of the platform configuration (`appsettings.json`, environment variables, or any other configuration provider). Each entry is matched to an incoming request by its shared secret.

```json
{
  "Punchout": {
    "Configurations": [
      {
        "StoreId": "B2B-store",
        "SenderDomain": "NetworkId",
        "SharedSecret": "<strong-random-secret>",
        "AllowedReturnUrls": [
          "https://acme.coupahost.com/punchout/checkout*"
        ],
        "TokenLifeTime": "00:15:00",
        "SessionLifeTime": "04:00:00"
      }
    ]
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `StoreId` | String | — | Store that serves the punchout session. The store must have a `Url` or `SecureUrl`, which is used to build the start page |
| `SharedSecret` | String | — | Value expected in `Header/Sender/Credential/SharedSecret`. Must be unique for each integration |
| `SenderDomain` | String | *(not checked)* | Expected `domain` attribute of `Header/Sender/Credential`. Leave empty to skip the check |
| `AllowedReturnUrls` | String[] | *(any URL)* | Allowed `BrowserFormPost` URLs, either exact or a prefix ending with `*`. If the list is empty, any URL is accepted |
| `TokenLifeTime` | TimeSpan | `00:15:00` | How long the start page URL can be redeemed |
| `SessionLifeTime` | TimeSpan | `04:00:00` | How long the buyer can shop after the setup request, which is also the access token lifetime |

### Store Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Punchout.Enabled` | Boolean | `false` | Turns on punchout mode for the store. This is a public setting, so the storefront can read it |

### Permissions

| Permission | Description |
|------------|-------------|
| `punchout:access` | Open the Punchout module |
| `punchout:read` | View punchout user mappings |
| `punchout:create` | Create punchout user mappings |
| `punchout:update` | Update punchout user mappings |
| `punchout:delete` | Delete punchout user mappings |

## Architecture

The module follows the layered architecture used across Virto Commerce platform modules:

```
┌─────────────────────────────────────────────────────────────┐
│  Web Layer (cXML & REST controllers, Admin UI, Module init) │
├─────────────────────────────────────────────────────────────┤
│  Experience API Layer (Profile xAPI pipeline middlewares)   │
├─────────────────────────────────────────────────────────────┤
│  Data Layer (cXML services, Setup, Sessions, Grant handler) │
├──────────┬──────────────┬──────────────┬────────────────────┤
│ SqlServer│    MySql     │  PostgreSql  │  DB Providers      │
├──────────┴──────────────┴──────────────┴────────────────────┤
│  Core Layer (cXML & domain models, interfaces, constants)   │
└─────────────────────────────────────────────────────────────┘
```

### PunchOut Setup Flow

1. The buyer clicks the supplier's catalog in the procurement system, for example Coupa, which then sends a cXML `PunchOutSetupRequest` to `POST api/punchout/cxml`.
2. `PunchoutController` deserializes the document with DTD processing and external entity resolution disabled. `CxmlRequestDispatcher` routes it to the handler that can process it, `CxmlPunchoutSetupRequestHandler`.
3. `PunchoutSetupMapper` converts the cXML document into a protocol-independent `PunchoutSetupRequest`.
4. `PunchoutSetupService` validates the request:
   * a configuration matches the shared secret, compared in constant time
   * the sender domain matches, if configured
   * the return URL is in the allow list, if configured
   * an **active** user mapping exists for `Header/Sender/Credential/Identity`
   * the configured store exists and has a storefront URL
5. A `PunchoutSession` is prepared, storing the buyer cookie, buyer identity and domain, return URL, session and token expiration dates, and the token hash. `IPunchoutHandler.HandleSetupAsync` can change or reject it.
6. The session is saved, and the cXML `PunchOutSetupResponse` returns the start page `{storefrontUrl}/punchout/{sessionToken}`.
7. The buyer's browser opens the start page. The storefront calls `connect/token` with `grant_type=punchout&session_token={sessionToken}`.
8. `PunchoutGrantTypeHandler` redeems the token atomically, checks that the user exists, is not locked out, and is allowed to sign in, and then issues an access token that expires with the session.
9. While the buyer shops, `PunchoutContactOrganizationsMiddleware` limits the contact's organizations to the current one.

### cXML Status Codes

| Setup outcome | cXML status |
|---------------|-------------|
| `Success` | `200 OK` (with `StartPage` URL) |
| `InvalidRequest` | `400 Bad Request` |
| `ReturnUrlNotAllowed` | `400 Bad Request` |
| `InvalidCredentials` | `401 Unauthorized` |
| `UserNotFound` | `401 Unauthorized` |
| Unsupported request type | `450 Not Implemented` |
| `StoreNotConfigured`, unexpected error | `500 Internal Server Error` |

The HTTP status code is always `200 OK`. Procurement systems read the outcome from the cXML `Status` element.

## Components

### Key Services

| Service | Interface | Responsibility |
|---------|-----------|----------------|
| `CxmlSerializer` | `ICxmlSerializer` | Secure XML (de)serialization of cXML documents, including the cXML DOCTYPE |
| `CxmlRequestDispatcher` | `ICxmlRequestDispatcher` | Single entry point that routes a cXML request to the matching handler, where the last registered handler wins |
| `CxmlPunchoutSetupRequestHandler` | `ICxmlRequestHandler` | Handles `PunchOutSetupRequest`: maps the document, calls the setup service, and maps the response |
| `CxmlResponseFactory` | `ICxmlResponseFactory` | Creates cXML response documents with a new `payloadID`, timestamp, and status |
| `PunchoutSetupMapper` | `IPunchoutSetupMapper` | Translates between cXML documents and `PunchoutSetupRequest` / `PunchoutSetupResult` |
| `PunchoutSetupService` | `IPunchoutSetupService` | Validates the setup request, creates the session, and builds the start page URL |
| `DefaultPunchoutHandler` | `IPunchoutHandler` | No-op extension point. Override it to adjust or reject a session before it is saved |
| `PunchoutSessionService` / `PunchoutSessionSearchService` | `IPunchoutSessionService` / `IPunchoutSessionSearchService` | CRUD and search for punchout sessions |
| `PunchoutSessionManagementService` | `IPunchoutSessionManagementService` | One-time, atomic redemption of session tokens |
| `PunchoutUserMappingService` / `PunchoutUserMappingSearchService` | `IPunchoutUserMappingService` / `IPunchoutUserMappingSearchService` | CRUD and search for external-identity-to-user mappings |
| `PunchoutGrantTypeHandler` | `GrantTypeHandlerBase` | `punchout` OAuth grant: redeems the session token and issues a session-bound access token |

### Domain Events

| Event | Raised when |
|-------|-------------|
| `PunchoutSessionChangingEvent` / `PunchoutSessionChangedEvent` | A punchout session is created, updated, or deleted |
| `PunchoutUserMappingChangingEvent` / `PunchoutUserMappingChangedEvent` | A user mapping is created, updated, or deleted |

### Data Model

| Table | Purpose |
|-------|---------|
| `PunchoutSession` | Punchout sessions: store, user, buyer cookie, buyer identity and domain, return URL, status, start page, session and token expiration dates, token hash (unique), and redemption flag |
| `PunchoutUserMapping` | Links an external identity (unique) to a platform user (`UserId`, `UserName`) and the contact (`MemberId`) that owns the account, with an active flag |

### REST API

#### cXML endpoint

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `api/punchout/cxml` | Anonymous (shared secret in the cXML header) | Process a cXML request (`PunchOutSetupRequest`) |
| `POST` | `connect/token` | `grant_type=punchout`, `session_token` | Redeem a punchout session token for an access token |

#### User mappings

Base route: `api/punchout-user-mappings`

| Method | Endpoint | Permission | Description |
|--------|----------|------------|-------------|
| `POST` | `/search` | `punchout:read` | Search user mappings |
| `GET` | `/{id}` | `punchout:read` | Get a user mapping by ID |
| `GET` | `/new` | `punchout:read` | Get a new, active user mapping template |
| `POST` | `/` | `punchout:create` | Create a user mapping |
| `PUT` | `/` | `punchout:update` | Update a user mapping |
| `DELETE` | `/?ids=` | `punchout:delete` | Delete user mappings |

## Dependencies

| Module | Purpose |
|--------|---------|
| `VirtoCommerce.Customer` | Contacts and organizations that own the mapped security accounts |
| `VirtoCommerce.Store` | Store storefront URL and the `Punchout.Enabled` store setting |
| `VirtoCommerce.ProfileExperienceApiModule` | `ContactOrganizationsContext` pipeline used to scope organizations for punchout users |

The storefront side of the flow (the `/punchout/{sessionToken}` start page and the token exchange) is implemented in [vc-frontend](https://github.com/VirtoCommerce/vc-frontend).

## Documentation

* [cXML protocol specification](https://cxml.org/)
* [View on GitHub](https://github.com/VirtoCommerce/vc-module-punchout/)

## References

* [Deployment](https://docs.virtocommerce.org/platform/developer-guide/Tutorials-and-How-tos/Tutorials/deploy-module-from-source-code/)
* [Installation](https://docs.virtocommerce.org/platform/user-guide/modules-installation/)
* [Home](https://virtocommerce.com)
* [Community](https://www.virtocommerce.org)
* [Download latest release](https://github.com/VirtoCommerce/vc-module-punchout/releases/latest)

## License

Copyright (c) Virto Solutions LTD.  All rights reserved.

This software is licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at http://virtocommerce.com/opensourcelicense.

Unless required by the applicable law or agreed to in written form, the software
distributed under the License is provided on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
