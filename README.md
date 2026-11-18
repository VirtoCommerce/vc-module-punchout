# Punchout Module

## Overview

The Punchout module connects Virto Commerce to e-procurement systems such as Coupa, SAP Ariba, and Oracle over the cXML PunchOut protocol. It covers the full punchout cycle:

1. **Setup**: a buyer starts shopping from inside their procurement system and is redirected to the Virto Commerce storefront, already signed in and working in the context of their organization. The module validates the incoming `PunchOutSetupRequest` against shared-secret configurations, maps the procurement-system identity to a platform user, opens a time-limited punchout session, and returns a one-time start page URL. The storefront exchanges that URL for an access token through a dedicated `punchout` OAuth grant.
2. **Requisition return**: when the buyer finishes shopping, the storefront calls the `createPunchoutRequisition` GraphQL mutation. The module builds a cXML `PunchOutOrderMessage` from the cart and returns it with the procurement system's return URL, so the storefront can post it back to the procurement system.
3. **Order**: after the requisition is approved, the procurement system sends a cXML `OrderRequest`. The module authenticates it, checks it against the returned requisition and the cart, and creates a customer order from the cart.

## Key Features

* **cXML PunchOut endpoint**: a single anonymous endpoint (`POST api/punchout/cxml`) accepts cXML documents and always answers with a cXML response. The outcome is carried in the `Status` element, as the protocol requires.
* **PunchOutSetupRequest support**: parses `Header/From`, `Header/To`, `Header/Sender`, `BuyerCookie`, `BrowserFormPost`, `Contact`, and `Extrinsic` elements. Request types without a registered handler return cXML status `450 Not Implemented`.
* **PunchOutOrderMessage support**: the `createPunchoutRequisition` mutation turns the items selected for checkout in the buyer's cart into a `PunchOutOrderMessage`. Each session can return a requisition only once, and every message sent to the buyer is stored.
* **OrderRequest support**: accepts `new` orders, checks them against the returned requisition and the cart (SKU, quantity, unit price, currency), applies the `ShipTo` and `BillTo` addresses and the configured default shipping and payment methods, and creates a customer order with the buyer's `orderID` as the purchase order number. `update` and `delete` orders return `450 Not Implemented`.
* **Idempotent order processing**: every authenticated `OrderRequest` is stored with its outcome. A document resent with the same `payloadID`, or the same purchase order resent with a new `payloadID`, returns the stored result instead of creating a second order. Concurrent duplicates are rejected by a unique index.
* **Shared-secret authentication**: the sender's `SharedSecret` is compared against every configured integration in constant time, with an optional check of the sender domain.
* **Return URL allow list**: restricts the `BrowserFormPost` URL to exact URLs or `*`-suffixed prefixes for each integration.
* **User mapping**: links an external identity from `Header/Sender/Credential/Identity` to a platform customer account. Mappings are managed from the contact details blade in the admin.
* **One-time session tokens**: each setup request issues a cryptographically random 32-byte token. The database stores only its SHA-256 hash, and the token is redeemed atomically, exactly once, before its lifetime ends (15 minutes by default).
* **`punchout` OAuth grant type**: `connect/token` with `grant_type=punchout` redeems the session token and issues an access token for the mapped user. The token is valid until the session expires (4 hours by default), carries `channelId` and `channelSessionId` claims, and comes without a refresh token.
* **Organization scoping**: when a request carries a punchout principal, the buyer's available organizations are narrowed to the current organization through the Profile Experience API pipeline.
* **Per-store switch**: setup and order requests are rejected unless the `Punchout.Enabled` setting is turned on for the configured store.
* **Extensibility**: each integration can use its own `IPunchoutHandler` to customize or reject the setup, the order message, and the order. New cXML request types can be added as `ICxmlRequestHandler` implementations, and models are created through `AbstractTypeFactory`.
* **Multi-database support**: SQL Server, MySQL, and PostgreSQL through dedicated EF Core provider assemblies.

## Configuration

### Punchout Integrations

Integrations are defined in the `Punchout` section of the platform configuration (`appsettings.json`, environment variables, or any other configuration provider). Each entry is matched to an incoming request by its shared secret.

```json
{
  "Punchout": {
    "Configurations": [
      {
        "Id": "my-config",
        "StoreId": "B2B-store",
        "SenderDomain": "NetworkId",
        "SharedSecret": "<strong-random-secret>",
        "AllowedReturnUrls": [
          "https://acme.coupahost.com/punchout/checkout*"
        ],
        "TokenLifeTime": "00:15:00",
        "SessionLifeTime": "04:00:00",
        "DefaultShipmentMethodCode": "FixedRate",
        "DefaultPaymentMethodCode": "DefaultManualPaymentMethod"
      }
    ]
  }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | String | — | Unique integration identifier. It is stored in the punchout session and in the order requests to find the integration in later transactions, so do not change it while sessions are active |
| `StoreId` | String | — | Store that serves the punchout session. The store must have the `Punchout.Enabled` setting turned on and a `Url` or `SecureUrl`, which is used to build the start page |
| `SharedSecret` | String | — | Value expected in `Header/Sender/Credential/SharedSecret` of the `PunchOutSetupRequest` and `OrderRequest`. Must be unique for each integration |
| `SenderDomain` | String | *(not checked)* | Expected `domain` attribute of `Header/Sender/Credential`. Leave empty to skip the check |
| `AllowedReturnUrls` | String[] | *(any URL)* | Allowed `BrowserFormPost` URLs, either exact or a prefix ending with `*`. If the list is empty, any URL is accepted |
| `TokenLifeTime` | TimeSpan | `00:15:00` | How long the start page URL can be redeemed |
| `SessionLifeTime` | TimeSpan | `04:00:00` | How long the buyer can shop and return the requisition after the setup request. This is also the access token lifetime |
| `DefaultShipmentMethodCode` | String | — | Code of the shipping method added to the cart when an `OrderRequest` is received. If empty or not available for the cart, the shipment is added without a shipping method |
| `DefaultPaymentMethodCode` | String | — | Code of the payment method added to the cart when an `OrderRequest` is received. If empty or not available for the cart, the payment is added without a payment method |
| `HandlerTypeName` | String | *(default handler)* | Name of the `IPunchoutHandler` registered with `AddPunchoutHandler<THandler>(name)` (the class name if `name` is omitted) that customizes this integration. If empty, the last `IPunchoutHandler` registered with `AddTransient<IPunchoutHandler, THandler>()` is used, so such a registration replaces the default handler for all integrations. If the named handler is not registered, the integration's transactions fail with a configuration error |

`Id`, `StoreId`, and `SharedSecret` are required, and `Id` and `SharedSecret` must be unique. The configurations are validated on the platform start, and the platform does not start if any of them is invalid.

### Store Settings

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Punchout.Enabled` | Boolean | `false` | Turns on punchout for the store. While it is off, setup requests, requisition returns, and order requests for the store are rejected. This is a public setting, so the storefront can read it |

### User Mappings

Each procurement-system user who punches out needs a mapping between their external identity (`Header/Sender/Credential/Identity`) and a platform security account. Open a contact in the admin and use the **Punchout user** widget on the contact details blade. A mapping is valid when:

* the external identity is not empty and is not used by another mapping
* the account belongs to the contact
* the account is a customer account and not an administrator

Only **active** mappings are used, and the mapping is checked again when the session token is redeemed.

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
┌──────────────────────────────────────────────────────────────┐
│  Web Layer (cXML & REST controllers, Admin UI, Module init)  │
├──────────────────────────────────────────────────────────────┤
│  Experience API Layer (createPunchoutRequisition mutation,   │
│  default handler, order message builder, Profile middleware) │
├──────────────────────────────────────────────────────────────┤
│  Data Layer (cXML services, processors, sessions, grant)     │
├──────────┬──────────────┬──────────────┬─────────────────────┤
│ SqlServer│    MySql     │  PostgreSql  │  DB Providers       │
├──────────┴──────────────┴──────────────┴─────────────────────┤
│  Core Layer (cXML & domain models, interfaces, constants)    │
└──────────────────────────────────────────────────────────────┘
```

| Project | Content |
|---------|---------|
| `VirtoCommerce.Punchout.Core` | cXML and domain models, service interfaces, events, constants, `AddPunchoutHandler` extension |
| `VirtoCommerce.Punchout.Data` | cXML serializer, dispatcher, request handlers and mappers, setup / order message / order request processors, CRUD and search services, `punchout` grant handler, validators, EF Core repository |
| `VirtoCommerce.Punchout.Data.SqlServer` / `.MySql` / `.PostgreSql` | Database migrations |
| `VirtoCommerce.Punchout.ExperienceApi` | `createPunchoutRequisition` GraphQL mutation, `DefaultPunchoutHandler`, `PunchoutOrderMessageBuilder`, organization scoping middleware |
| `VirtoCommerce.Punchout.Web` | Module initialization, REST controllers, admin UI, localizations |

### PunchOut Setup Flow

1. The buyer clicks the supplier's catalog in the procurement system, for example Coupa, which then sends a cXML `PunchOutSetupRequest` to `POST api/punchout/cxml`.
2. `PunchoutController` deserializes the document with DTD processing and external entity resolution disabled. `CxmlRequestDispatcher` routes it to the handler that can process it, `CxmlPunchoutSetupRequestHandler`.
3. `PunchoutSetupMapper` converts the cXML document into a protocol-independent `PunchoutSetupRequest`.
4. `PunchoutSetupProcessor` validates the request:
   * a configuration matches the shared secret, compared in constant time
   * the sender domain matches, if configured
   * the return URL is in the allow list, if configured
   * an **active** user mapping exists for `Header/Sender/Credential/Identity`
   * the configured store exists, has punchout enabled, and has a storefront URL
5. A `PunchoutSession` is prepared, storing the configuration, user, buyer cookie, buyer, supplier, and sender identities and domains, return URL, session and token expiration dates, and the token hash. The integration's `IPunchoutHandler.HandleSetupAsync` can change or reject it.
6. The session is saved, and the cXML `PunchOutSetupResponse` returns the start page `{storefrontUrl}/punchout/{sessionToken}`.
7. The buyer's browser opens the start page. The storefront calls `connect/token` with `grant_type=punchout&session_token={sessionToken}`.
8. `PunchoutGrantTypeHandler` redeems the token atomically, checks that the session has not expired, the user mapping is still active, and the user exists, is a customer and not an administrator, is not locked out, and is allowed to sign in. It then issues an access token that expires with the session.
9. While the buyer shops, `PunchoutContactOrganizationsMiddleware` limits the contact's organizations to the current one.

### Requisition Return Flow (PunchOutOrderMessage)

1. The buyer selects the cart items for checkout and clicks the button that returns the requisition. The storefront calls the `createPunchoutRequisition` mutation on the `graphql/punchout` endpoint with the punchout access token.
2. `CreatePunchoutRequisitionCommandHandler` takes the session ID from the `channelSessionId` claim and calls `PunchoutOrderMessageProcessor`.
3. The processor checks that the session exists and belongs to the current user and store, the store has punchout enabled, the session is still active and has not been returned, and its configuration still exists.
4. The integration's `IPunchoutHandler.HandleOrderMessageAsync` builds the cXML. `DefaultPunchoutHandler` loads the buyer's cart and fails with `CartNotFound` or `CartIsEmpty` if there is nothing to return. Otherwise `PunchoutOrderMessageBuilder` builds the `PunchOutOrderMessage`:
   * `From` and `Sender` carry the supplier credentials from the setup request, and `To` carries the buyer credentials
   * `BuyerCookie` is the one received in the setup request, and `operationAllowed` is `create`
   * `Total` is the sum of the line extended prices. Cart-level discounts, shipping, and taxes are not included
   * each `ItemIn` has the SKU as `SupplierPartID`, `{lineItemId};{sessionId}` as `SupplierPartAuxiliaryID`, the placed price as `UnitPrice`, the product measure unit (`EA` by default), and a placeholder classification (`Custom` / `00000000`)
5. The session is atomically switched from `Active` to `Returned`, so it cannot return a second requisition, and the cXML is stored in `PunchoutOrderMessage`.
6. The mutation returns the procurement system's return URL, the form field name (`cxml-urlencoded`), and the cXML. The storefront posts the form to the procurement system. If the requisition cannot be returned, only an error code is returned and the details are logged.

### Order Flow (OrderRequest)

1. After the requisition is approved, the procurement system sends a cXML `OrderRequest` to `POST api/punchout/cxml`. `CxmlPunchoutOrderRequestHandler` and `PunchoutOrderMapper` convert it into a `PunchoutOrderRequestData`, including addresses, contacts, items, classifications, and accounting distributions.
2. `PunchoutOrderRequestProcessor` authenticates the request in the same way as the setup request: shared secret, sender domain, and an active user mapping for `Header/Sender/Credential/Identity`. Requests that do not match any configuration are not stored.
3. The processor looks for an earlier request with the same `payloadID`, or a completed or in-progress `new` order with the same `orderID`. A completed request returns its stored result, and a request still in progress returns `OrderInProgress`. Failed and rejected requests are processed again.
4. The order is validated:
   * the store exists and has punchout enabled
   * the order type is `new` (the default when the `type` attribute is missing)
   * `payloadID` (up to 512 characters), `orderID` (up to 256 characters), the `Total` currency, and items are present
   * every item has a `SupplierPartID`, a positive quantity, and a non-negative unit price in the order currency
   * the order currency is available in the store
   * all items reference the same punchout session in `SupplierPartAuxiliaryID`. Without a session reference, the user's last returned session is used
   * the session has returned a requisition for the same user and integration, and every item is in that requisition
5. The request is stored with the `Processing` status. The unique index on the configuration and `payloadID` stops a concurrent duplicate.
6. The integration's `IPunchoutHandler.HandleOrderRequestAsync` creates the order. `DefaultPunchoutHandler`:
   * requires a unique `SupplierPartAuxiliaryID` on every line and finds the user's cart that contains those line items
   * reconciles the order with the cart: currency, SKU, quantity, and unit price rounded to two decimals must match, because the cart wins
   * selects only the ordered items for checkout, sets the purchase order number to `orderID`, and adds the `ShipTo` and `BillTo` addresses (ISO alpha-2 country codes are converted to alpha-3, and `DeliverTo` becomes the first and last name)
   * adds a shipment with `DefaultShipmentMethodCode` and a payment with `DefaultPaymentMethodCode` for the cart total, replacing what the buyer selected in the storefront
   * validates the cart, saves it, creates the order with `CreateOrderFromCartCommand`, and clears the cart
7. The request is stored as `Completed` with the customer order ID, `Rejected` if the order content is invalid, or `Failed` for other errors. The cXML response carries the outcome.

### cXML Status Codes

The HTTP status code is always `200 OK`. Procurement systems read the outcome from the cXML `Status` element.

| Outcome | cXML status |
|---------|-------------|
| Not a well-formed cXML document, no `Request` element | `400 Bad Request` |
| Unsupported request type | `450 Not Implemented` |
| Unexpected error | `500 Internal Server Error` |

#### PunchOutSetupRequest

| Setup outcome | cXML status |
|---------------|-------------|
| `Success` | `200 OK` (with `StartPage` URL) |
| `InvalidRequest`, `ReturnUrlNotAllowed` | `400 Bad Request` |
| `InvalidCredentials`, `UserNotFound` | `401 Unauthorized` |
| `StoreNotConfigured`, `ConfigurationError`, `SetupError` | `500 Internal Server Error` |

#### OrderRequest

| Order outcome | cXML status |
|---------------|-------------|
| `Success` | `200 OK` |
| `InvalidRequest`, `CurrencyNotSupported` | `400 Bad Request` |
| `InvalidCredentials`, `UserNotFound` | `401 Unauthorized` |
| `UnsupportedOrderType` | `450 Not Implemented` |
| `OrderInProgress` | `500 Internal Server Error` (the buyer can retry later) |
| `StoreNotConfigured`, `ConfigurationError`, `OrderError` | `500 Internal Server Error` |

The status text of a rejected order explains the reason, for example the order line that does not match the cart.

#### Requisition return error codes

The `createPunchoutRequisition` mutation returns one of these codes in `errorCode`: `SessionNotFound`, `SessionExpired`, `SessionReturned`, `StoreNotConfigured`, `ConfigurationError`, `MessageError`, and, from `DefaultPunchoutHandler`, `CartNotFound` and `CartIsEmpty`.

## Extensibility

### Punchout handler

`IPunchoutHandler` is the business extension point of each transaction:

| Method | Called | Default behavior |
|--------|--------|------------------|
| `HandleSetupAsync` | Before the session is saved | Nothing |
| `HandleOrderMessageAsync` | Before the session is marked as returned | Builds the `PunchOutOrderMessage` from the cart |
| `HandleOrderRequestAsync` | After the order is validated | Creates the customer order from the cart |

A handler calls `context.Fail(code, message)` to stop the transaction. Nothing is persisted, and the code is returned to the buyer.

To customize one integration, derive from `DefaultPunchoutHandler`, register it with a name, and set that name in the integration's `HandlerTypeName`:

```csharp
serviceCollection.AddPunchoutHandler<AcmePunchoutHandler>("Acme");
```

To replace the default handler for all integrations without `HandlerTypeName`, register it as `IPunchoutHandler`:

```csharp
serviceCollection.AddTransient<IPunchoutHandler, MyPunchoutHandler>();
```

`PunchoutOrderMessageBuilder` (`IPunchoutOrderMessageBuilder`) can be overridden to change the `PunchOutOrderMessage` content, for example to send real UNSPSC classifications instead of the placeholder.

### cXML request types

Register an `ICxmlRequestHandler` to support another cXML request type or to replace a built-in handler. The dispatcher uses the last registered handler whose `CanHandle` returns `true`.

## Components

### Key Services

| Service | Interface | Responsibility |
|---------|-----------|----------------|
| `CxmlSerializer` | `ICxmlSerializer` | Secure XML (de)serialization of cXML documents, including the cXML DOCTYPE |
| `CxmlRequestDispatcher` | `ICxmlRequestDispatcher` | Single entry point that routes a cXML request to the matching handler, where the last registered handler wins |
| `CxmlPunchoutSetupRequestHandler` | `ICxmlRequestHandler` | Handles `PunchOutSetupRequest`: maps the document, calls the setup processor, and maps the response |
| `CxmlPunchoutOrderRequestHandler` | `ICxmlRequestHandler` | Handles `OrderRequest`: maps the document, calls the order request processor, and maps the response |
| `CxmlResponseFactory` | `ICxmlResponseFactory` | Creates cXML documents and responses with a new `payloadID`, timestamp, and status |
| `PunchoutSetupMapper` | `IPunchoutSetupMapper` | Translates between cXML documents and `PunchoutSetupRequest` / `PunchoutSetupResult` |
| `PunchoutOrderMapper` | `IPunchoutOrderMapper` | Translates between cXML documents and `PunchoutOrderRequestData` / `PunchoutOrderResult` |
| `PunchoutSetupProcessor` | `IPunchoutSetupProcessor` | Validates the setup request, creates the session, and builds the start page URL |
| `PunchoutOrderMessageProcessor` | `IPunchoutOrderMessageProcessor` | Validates the session, calls the handler to build the `PunchOutOrderMessage`, marks the session as returned, and stores the message |
| `PunchoutOrderRequestProcessor` | `IPunchoutOrderRequestProcessor` | Authenticates and validates the `OrderRequest`, handles duplicates, calls the handler, and stores the outcome |
| `PunchoutHandlerFactory` | `IPunchoutHandlerFactory` | Resolves the `IPunchoutHandler` of an integration by `HandlerTypeName` |
| `DefaultPunchoutHandler` | `IPunchoutHandler` | Default transaction logic based on the Cart and Order Experience APIs |
| `PunchoutOrderMessageBuilder` | `IPunchoutOrderMessageBuilder` | Builds the cXML `PunchOutOrderMessage` from the session and the cart |
| `PunchoutSessionService` / `PunchoutSessionSearchService` | `IPunchoutSessionService` / `IPunchoutSessionSearchService` | CRUD and search for punchout sessions |
| `PunchoutSessionManagementService` | `IPunchoutSessionManagementService` | Atomic, one-time redemption of session tokens and switching sessions to `Returned` |
| `PunchoutOrderMessageService` / `PunchoutOrderMessageSearchService` | `IPunchoutOrderMessageService` / `IPunchoutOrderMessageSearchService` | CRUD and search for the stored order messages |
| `PunchoutOrderRequestService` / `PunchoutOrderRequestSearchService` | `IPunchoutOrderRequestService` / `IPunchoutOrderRequestSearchService` | CRUD and search for the stored order requests |
| `PunchoutUserMappingService` / `PunchoutUserMappingSearchService` | `IPunchoutUserMappingService` / `IPunchoutUserMappingSearchService` | CRUD and search for external-identity-to-user mappings |
| `PunchoutGrantTypeHandler` | `GrantTypeHandlerBase` | `punchout` OAuth grant: redeems the session token and issues a session-bound access token |

### Domain Events

| Event | Raised when |
|-------|-------------|
| `PunchoutSessionChangingEvent` / `PunchoutSessionChangedEvent` | A punchout session is created, updated, or deleted |
| `PunchoutUserMappingChangingEvent` / `PunchoutUserMappingChangedEvent` | A user mapping is created, updated, or deleted |
| `PunchoutOrderMessageChangingEvent` / `PunchoutOrderMessageChangedEvent` | An order message is created, updated, or deleted |
| `PunchoutOrderRequestChangingEvent` / `PunchoutOrderRequestChangedEvent` | An order request is created, updated, or deleted |

Token redemption and the switch to `Returned` update the session directly in the database and do not raise events.

### Data Model

| Table | Purpose |
|-------|---------|
| `PunchoutSession` | Punchout sessions: store, configuration, user, buyer cookie, buyer, supplier, and sender identities and domains, return URL, status (`Active` or `Returned`), start page, session and token expiration dates, token hash (unique), and redemption flag |
| `PunchoutUserMapping` | Links an external identity (unique) to a platform user (`UserId`, `UserName`) and the contact (`MemberId`) that owns the account, with an active flag |
| `PunchoutOrderMessage` | cXML of the `PunchOutOrderMessage` returned for a session. Deleted together with the session |
| `PunchoutOrderRequest` | Received `OrderRequest` documents: configuration, store, `payloadID` (unique within the configuration), sender and buyer identities, user, order type, purchase order number and date, currency, total, created customer order, status (`Processing`, `Completed`, `Failed`, or `Rejected`), and error code and message |

### REST API

#### cXML endpoint

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `api/punchout/cxml` | Anonymous (shared secret in the cXML header) | Process a cXML request (`PunchOutSetupRequest` or `OrderRequest`) |
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

### GraphQL API

The module registers its own schema at `graphql/punchout`.

| Operation | Type | Description |
|-----------|------|-------------|
| `createPunchoutRequisition` | Mutation | Builds the `PunchOutOrderMessage` for the punchout session of the current access token and returns the data the storefront posts to the procurement system |

```graphql
mutation {
  createPunchoutRequisition(
    command: { storeId: "B2B-store", currencyCode: "USD", cultureName: "en-US" }
  ) {
    url
    formField
    cxml
    errorCode
  }
}
```

`storeId` is required. `currencyCode` and `cultureName` default to the store's default currency and language. The session ID is taken from the access token, so the mutation works only with a token issued by the `punchout` grant.

## Dependencies

| Module | Purpose |
|--------|---------|
| `VirtoCommerce.Customer` | Contacts and organizations that own the mapped security accounts |
| `VirtoCommerce.Store` | Store storefront URL, currencies, and the `Punchout.Enabled` store setting |
| `VirtoCommerce.ProfileExperienceApiModule` | `ContactOrganizationsContext` pipeline used to scope organizations for punchout users |
| `VirtoCommerce.XCart` | Cart loading and search, cart changes, and available shipping and payment methods |
| `VirtoCommerce.XOrder` | Creating the customer order from the cart |

The storefront side of the flow (the `/punchout/{sessionToken}` start page, the token exchange, and posting the requisition back to the procurement system) is implemented in [vc-frontend](https://github.com/VirtoCommerce/vc-frontend).

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
