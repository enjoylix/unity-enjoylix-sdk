# CHANGELOG

## [2.0.14] — Portal Environment Detection — 2026-09-11

### Fixed
- **Portal detection no longer depends on `document.referrer`** — the referrer is a privacy-sensitive field that browsers strip in private/incognito modes, under a restrictive `Referrer-Policy`, and on direct navigation, so a game on the portal could silently fall back to standalone behaviour: `PlayerPrefs` session storage instead of in-memory, and `LoginOnStartAsync` opening the guest/OpenID flow instead of the portal `postMessage` handshake (the "Save progress" button showing up on the portal). The build is now considered to be on the portal when the page is embedded in an iframe (`window.self !== window.top`), the same rule the JS SDK uses. `document.referrer` and `location.ancestorOrigins` are still logged for diagnostics but no longer decide anything.

### Added
- **`EnjoylixConfig.WebPortalMode`** — `Auto` (default, iframe check above), `Portal` (always portal) or `Standalone` (never portal). Set `Standalone` if you embed the WebGL build in an iframe on your own site, otherwise `LoginOnStartAsync` will wait for a portal answer that never comes and time out after 2 minutes. Ignored outside WebGL player builds.
- **`EnjoylixSdk.IsWebPortal`** — the resolved value, so the game can branch its own UI on it (e.g. hide "save progress" prompts on the portal).
- **`Enjoylix_IsEmbeddedInIframe` / `Enjoylix_GetAncestorOrigins` (JS)** — new jslib functions backing the detection.

### Changed
- `AuthStorageFactory.Create` takes the `EnjoylixConfig` instead of the salt string; `EnvironmentDetector.IsWebPortal` takes a `PortalMode`. Both are internal — games using `EnjoylixSdk` need no changes.

---

## [2.0.13] — Auth Reliability, Attribution Contract & Signed Purchase — 2026-08-26

### Added
- **Auth requests retry on transient failures** — `guest_login`, `GET /me` and `POST /api/v2/auth/<game_name>/token` go through a shared `HttpRetry`: 3 attempts, 1s/2s backoff, on `ConnectionError`, code `0`, `408`, `429` and `5xx`. Backend answers (`400`, `401`, `409`, `422`) still throw on the first attempt, so the re-login policy and the guest-409 branch behave as before. `RefreshToken` is deliberately not retried — `TryRefreshOrReloginAsync` has its own recovery path.
- **One automatic retry on `401`** for `PurchaseCreateAsync` and `IsAgeVerificationNeededAsync` — the SDK refreshes the session and repeats the request once, so a wrong device clock or a token revoked mid-session recovers on its own. Safe for purchases: the browser opens only after create succeeds.
- **`AuthService.ForceRefreshAsync(CancellationToken)`** — refreshes without consulting the cached session, for use right after the server rejects a token.
- **`POST /api/v1/purchase/create` is signed** — it carries `X-Signature`, the HMAC-SHA256 of the request body in lowercase hex, so a coin spend cannot be opened with a stolen access token alone. The body is serialized canonically (sorted keys, no whitespace, non-ASCII escaped as `\uXXXX`) so the digest the backend recomputes matches; the fields and values are otherwise unchanged.
  - The key is the new **`RequestSignSecret`** field on the `EnjoylixEnvironment` asset — one per environment — shipped as the placeholder `YOUR_SIGN_KEY_PROVIDED_BY_ENJOYLIX_TEAM`. Replace it with the key the Enjoylix team issues you; leaving it empty sends the request unsigned, exactly as before, and logs a warning on startup.
  - The backend only logs a bad signature today and starts rejecting with `401` once every client signs — fill the secret in before then. No other endpoint is signed.

### Changed
- **`EnsureValidTokenAsync` no longer probes `GET /users/me`** — the access token is a JWT, so validity is decided locally from its `exp` (usable while `Me` belongs to the current token and `exp` is more than 30s away; an unreadable `exp` falls back to the old behaviour). `me` is still called where it establishes identity: after guest login, after an auth-code exchange and after a refresh. A cold start with an age-verification check: three `me` requests become one.
- **`auth_id` is resolved at send time** from `Me.id`, falling back to `GuestId`, instead of being pushed into the attribution context — this fixes an empty `auth_id` on events sent before the first `/me` of a warm start, and stale ids on events after `Logout()`. New `AttributionService.ClearAuthId()` drops a game-supplied override.
- **`platform` comes from the `EnjoylixSdk` constructor argument** (`ANDROID_SELF` vs `ENJOYLIX_ANDROID`) rather than `Application.platform`, which reports `Android` for both, and is also the default for `platform_id`. An empty argument falls back to `Application.platform.ToString()` as before.
- **`EnjoylixNetworkException`** — thrown instead of `EnjoylixApiException` when a request exhausts its attempts for a transient reason. It derives from it, so existing `catch` blocks keep working, and adds `Attempts`. Error messages now also carry `UnityWebRequest.Result` next to the response code, which tells an aborted request apart from one blocked by CORS.
- Constructors: `AttributionService` takes platform 3rd and the `auth_id` resolver 4th, moving `SessionQueryParams` to 5th; `PurchaseApiClient` takes the sign secret as an optional 2nd argument. Only code constructing them directly is affected — games using `EnjoylixSdk` need no changes.

### Fixed
- **WebGL: `Task.Delay` never resumes**, which silently broke four flows in browser builds — the retry backoff would have hung the auth flow, popup dismiss detection stopped after its first check so a closed login/payment tab hung with no error (AC-9936), the purchase dismiss reconciliation added in 2.0.7 never ran, and the session `ping` was sent once and never repeated. All four now use a coroutine-driven delay that ticks on the Unity player loop.
- **Popup and portal flow timeouts are enforced** and raise the new **`FlowTimeoutException`** (carrying the flow `Context` and the `Timeout`) — 5 minutes for `LoginOpenIdAsync`, 2 minutes for `LoginOnStartWeb`, which used to hang startup forever when the portal never answered. The wait itself was previously unguarded, and `CancellationTokenSource.CancelAfter` does not fire in WebGL either.
- **A JWT is no longer sent as `auth_id`** — the field is the Enjoylix user id, but access tokens were reaching the unauthenticated analytics endpoint in production. Values shaped like a JWT (`ey…` with exactly two dots) are dropped with a `Debug.LogError` and the event goes out without `auth_id`; hex ids, UUIDs and ids merely starting with `ey` pass through.
- **`currency_code` is validated against ISO 4217** — trimmed and upper-cased, and anything that is not three letters logs a `Debug.LogError` and falls back to `USD`. Display strings such as `$19.99` were reaching the column, making it unusable for grouping; `amount` is already in USD.
- An empty `auth_id` logs a `Debug.LogWarning` naming the event instead of going out silently.

---

## [2.0.12] — Launch Authorization on startup — 2026-08-18

### Added
- **insta_reg** query parameter to do automatic auhtentication via browser session on platforms

## [2.0.11] — Launch Query Params on the Token Request — 2026-08-11

### Added
- **The query params the game was launched with are now sent with the token request** — they are appended to `POST /api/v2/auth/<game_name>/token`, so a game opened as `…/ac/?utm_source=facebook&utm_medium=cpc&utm_campaign=summer_sale` now requests its tokens from `/api/v2/auth/ac/token?utm_source=facebook&utm_medium=cpc&utm_campaign=summer_sale`. The portal passes its own query string on to the game URL, and the backend needs it on the request that creates the in-game registration to attribute that registration to the campaign the player came from — without it every in-game registration looks organic.
  - The params are read from `Application.absoluteURL` when `EnjoylixSdk` is constructed — the same launch URL the SDK already used for the `query_params` of attribution events. Both now read one shared `SessionQueryParams` object, so the token request and the attribution events always report the same set. Nothing about the attribution events themselves changes.
  - Every param is forwarded as-is — no allow-list, no filtering — and escaped by the same code that builds every other SDK url. If the launch URL has no query string, the request is unchanged; that is the case on mobile and in the Editor, where `Application.absoluteURL` is empty.
  - A game that resolves its launch url itself can still supply it with `UpdateAttributionContext(new AttributionContext { RawDeeplinkUrl = url })`. From that point on its query string is used for both the token request and the attribution events.
  - Only `/api/v2/auth/<game_name>/token` is affected. The other two endpoints of the funnel task, `/api/v2/auth/login` and `/api/v1/auth/token`, are portal endpoints that the Unity SDK never calls.

### Changed
- `UrlBuilder` takes an optional `SessionQueryParams` as its fourth constructor argument, and `AttributionService` as its third. Both keep the previous behaviour when omitted, so only code constructing them directly is affected — games using `EnjoylixSdk` need no changes.

### Removed
- **`LoginId`** — dropped from `EnjoylixSdk` and `AuthService`, along with the internal `LoginIdUtils`. It shipped in 2.0.10 as `"OPENID" + md5(EnjoylixId)`, but nothing in the SDK ever sent or stored it and no game reads it — the game backend resolves the login from the `auth_ticket` it validates. Keeping it meant carrying a copy of the backend's `OpenIDUser.generate_login_id` in the client, where a change on the server side would have gone unnoticed until auth broke. `CreateGameSessionTicketAsync()` and `EnjoylixId` are unaffected, and the value is a one-line derivation from `EnjoylixId` for anyone who still needs it.
---

## [2.0.10] — Enjoylix Auth Ticket for the Game Backend — 2026-08-03

### Added
- **`CreateGameSessionTicketAsync()`** — new method on `EnjoylixSdk`. Requests a one-time `auth_ticket` from openid-service (`POST /api/v2/auth/game_session`, with `game_name` taken from `EnjoylixConfig.ProjectName`) and returns the raw `session_token`. It ensures a valid access token first, so it is safe to call at any point after login. The ticket is scoped to the game, expires after ~60s, and is burned by the game server on first validation, so the SDK never caches it — every call mints a fresh one. Pass it to the game backend immediately; if the backend rejects a ticket, call this again instead of reusing the old value.
- **`LoginId`** — new property on `EnjoylixSdk`. Returns `"OPENID" + md5(EnjoylixId)` in lowercase hex — the identifier the game backend expects alongside the ticket — or `null` while no session exists. The formula mirrors the game backend's `OpenIDUser.generate_login_id` and is verified against the RFC 1321 MD5 test vectors.

### Changed
- **`AuthService` constructor** now takes `EnjoylixConfig` instead of `string encryptionSalt`, mirroring `PurchaseService`. It needs `ProjectName` for the `game_name` of a session ticket in addition to the storage salt. Code constructing `AuthService` directly must pass the config object; users of `EnjoylixSdk` are unaffected.

---

## [2.0.9] — Session Ping — 2026-07-22

### Added
- **`partner` and `player_id` are now provided by the game** — both fields were going out empty because the SDK had no way to resolve them. They are purely client-side now: the SDK no longer reads `partner` from the launch deep link query and never derives either value on its own.
  - `partner` is a build-time constant agreed at the integration stage, so it moved to a new `Partner` field on the `EnjoylixConfig` asset. Fill it in once — no integration code required.
  - `player_id` is set at runtime via the new `SetPlayerId(string)` on `EnjoylixSdk`, since the game only knows its profile id after it resolves it. `SetPartner(string)` is also available to override the config value.
  - Both are exposed on `AttributionContext` as `Partner` / `PlayerId` and can be set through `UpdateAttributionContext` as well. Values persist until changed and are attached to every attribution event; `null` or an empty string is ignored and keeps the previous value.
  - **`SetPlayerId()` is what releases the session `ping`** — see below.
- **Session `ping` attribution event** — the SDK now reports that a player is online. The `ping` starts once the SDK has both `auth_id` and `player_id`, then repeats every 6 hours while the game is running. The payload is a standard attribution event (`timestamp`, `player_id`, `auth_id`, `partner`, `platform`) with an empty `event_data: {}`.
  - The integration chain is `Enjoylix auth` → `game resolves its profile` → `SetPlayerId()` → first `ping`. A player id only exists after the player has authenticated, so the `ping` waits for `SetPlayerId()` and is sent on that call — nothing goes out before it, since a `ping` without `player_id` is not usable on the backend. Order does not matter: whichever identifier arrives last starts the schedule. If the game never calls `SetPlayerId()`, no `ping` is sent.
  - `auth_id` is filled from `AuthService.ApplySession` via the new `OnSessionApplied` event, so it does not depend on the game calling `TrackLogin()`. `ApplySession` is the single point every entry path goes through after the account is confirmed with `GET /me` — explicit logins, a still-valid stored access token, and a refresh-token re-login are all covered.
  - Starting is idempotent per account — repeated `EnsureValidTokenAsync` calls and token refreshes do **not** reset the 6-hour interval. It restarts only when the account id changes or after `Logout()`.
  - The interval is wall-clock based (`DateTime.UtcNow`), so it keeps counting while the game is minimized or in a background tab. If the OS suspended the app when a ping came due, the ping is sent as soon as the app resumes and the next one is scheduled from that moment.
  - The schedule runs as a cancellable async loop (`AttributionPingScheduler`) — no `MonoBehaviour`, no `GameObject` added to the scene. It is cancelled by `Logout()` and by `Application.quitting` (which also covers leaving Play Mode in the Editor).
  - `Logout()` stops the schedule.

---

## [2.0.8] — Caller Identity Header — 2026-07-17

### Added
- **Caller identity header** — the SDK now tells the backend which client it is by adding an `X-Caller-Identity: unity-sdk/<version>` header to every request (e.g. `unity-sdk/2.0.8`). This helps identify Unity SDK traffic and the SDK version in use. It works automatically — no setup needed.

### Fixed
- **Coin spend safety check aborted before it could run** — the `coin_spend_status` reconciliation added in 2.0.7 reused the caller's `CancellationToken`. Since the game typically cancels that token as soon as the browser/popup closes — exactly when the dismiss path fires — the check was frequently cancelled before the request left the client, so completed coin spends were still reported as `UserDismissedException`. `TryReconcileAsync` now runs on its own `CancellationTokenSource` with a 10-second timeout, independent of the caller's token. A timeout is still treated as "not completed" and rethrows the original `UserDismissedException`. The warning log now also includes the exception type.

---

## [2.0.7] — Purchase Dismiss Server Reconciliation — 2026-07-14

### Fixed
- **False-dismiss on Android for deep-link-unreliable providers (e.g. Antilopay/SBP)** — when a purchase would be reported as `UserDismissedException` (browser regained focus without a result deep link within the grace period), `PurchaseService` now makes a single safety-net query to the new `GET /api/v1/purchase/coin_spend_status` endpoint (keyed on `game_name` + `game_transaction_id`) before giving up. If the server reports the coin spend as `completed`, the SDK returns a successful `PaymentResult` instead of throwing. Any other status, a 404, or a network error is treated as not completed and the original `UserDismissedException` is rethrown. Applies to all platforms since it hooks the platform-agnostic dismiss path.

---

## [2.0.5] — IsKnownFullAccount & Guest 409 Fix — 2026-06-17

### Added
- **`IsKnownFullAccount`** — new property on `EnjoylixSdk`. Returns `true` if the persisted `is_guest` flag is `false`, meaning the device was previously linked to a full account. Available immediately on startup without an active session.

### Fixed
- **`LoginGuestAsync` 409 state** — when the server returns HTTP 409 (device already linked to a full account), the SDK now persists `is_guest = false` and clears stored tokens before rethrowing. This ensures `ReLoginByPolicyAsync` skips the guest path on the next launch and prevents an infinite re-login loop.

---

## [2.0.4] — Login Cycle Fix for Age-Verified Users — 2026-06-10

### Fixed
- **Re-login cycle broken for OpenID (age-verified) users** — `ReLoginByPolicyAsync` no longer silently reopens the browser when an OpenID session expires. Instead, the SDK clears stored tokens and throws `InvalidOperationException`, forcing a clean re-login on the next explicit user action.
- **409 re-login cycle on guest re-login** — when a guest token returns HTTP 409 (device already linked to a full OpenID account), the SDK now marks the user as non-guest, clears tokens, and rethrows rather than falling back to an infinite browser-login loop.
- **`is_guest` flag lost between sessions** — `UpdateSession` now persists the `is_guest` flag to storage on every session update. On a fresh page/app load, `ReLoginByPolicyAsync` reads the persisted flag to determine the correct re-login policy.
- **Login endpoint** — `Endpoints.Auth.LoginPage` corrected from `"/"` to `"/login"`.

### Changed
- `IAuthStorage` gains two new members: `LoadIsGuest()` and `SaveIsGuest(bool)`. Both `PlayerPrefsAuthStorage` and `InMemoryAuthStorage` implement them; `PlayerPrefsAuthStorage.Clear()` now also removes the `enjoylix_is_guest` key.

---

## [2.0.3] — Deeplink Race Condition Fix — 2026-05-29

### Fixed
- **Android deeplink race condition** — `WebBrowserService` now waits 700 ms after `OnApplicationFocus` before firing the dismissed callback. This covers the observed 100–300 ms window on Android where `deepLinkActivated` arrives after focus is restored, preventing a false-dismiss when the user actually completed the auth/purchase flow via deeplink.

---

## [2.0.2] — Refresh Token, PKCE, Portal Environment Detection & Auth Storage  — 2026-05-21

### Added
- **`EnvironmentDetector.IsWebPortal()`** — detects whether the game runs inside the Enjoylix portal by inspecting `document.referrer` (WebGL only; always returns `false` in Editor/native builds).
- **`AuthStorageFactory`** — selects `InMemoryAuthStorage` when running in the Enjoylix portal (WebGL), and `PlayerPrefsAuthStorage` for all other platforms and contexts.
- **`InMemoryAuthStorage`** — session data (guest ID, access token, refresh token) is stored in memory for the duration of a WebGL portal session. `Clear()` wipes user-bound fields but preserves device ID, matching `PlayerPrefsAuthStorage` behaviour.
- **`LoginOnStartWeb(CancellationToken ct = default)`** — new public method on `EnjoylixSdk`. Triggers an automatic login handshake when the game runs as a WebGL iframe inside the Enjoylix portal. Uses PKCE + `postMessage` to request an auth code from the parent portal window, then exchanges it for a token pair.
- **`Enjoylix_SendPortalLoginRequest` (JS)** — new jslib function that sends an `enjoylix_portal_login_request` message to `window.parent` and awaits the auth-code response via `window.addEventListener("message", ...)`.
- **`Enjoylix_GetDocumentReferrer` (JS)** — new jslib function that returns `document.referrer` as a UTF-8 string for environment detection.
- **Refresh token** — after the first login the SDK silently restores the session on next launch without reopening the browser.
- **PKCE** — auth flow now uses code_challenge / code_verifier for improved security. Requires `EncryptionSalt` to be set in `EnjoylixConfig`.
- **Portal auto-login** (`LoginOnStartWeb`) — automatic login for games running inside the Enjoylix portal as a WebGL iframe.
- **Automatic storage selection** — session is kept in memory on the portal and in PlayerPrefs everywhere else.

### Changed
- `EnjoylixConfig` has a new required `EncryptionSalt` field used to derive the refresh token encryption key.
- `AuthService` now uses `AuthStorageFactory.Create()` instead of directly instantiating `PlayerPrefsAuthStorage`, enabling automatic storage selection per runtime environment.

---

## [2.0.1] — Age Verification Platform Parameter — 2026-04-15

### Changed
- Platform is now included in the age verification check request.

---

## [2.0.0] — Major Refactoring & New Features — 2026-04-14

### Added
- **Age Verification** — browser-based age verification flow (`IsAgeVerificationNeededAsync`, `StartAgeVerificationAsync`).
- **`UserDismissedException`** — thrown when the user returns from the browser without completing a flow.
- Timeout parameter added to `LoginOpenIdAsync` and `LinkGuestAsync`.

### Changed
- `platform` is now a required constructor parameter on `EnjoylixSdk`.
- All SDK methods are now exposed directly on `EnjoylixSdk` — `sdk.Auth.X` / `sdk.Purchase.X` no longer exist.
- Popup blocking unified into a single `OnPopupBlocked` event on `EnjoylixSdk`.
- `Initialize()` is now synchronous.

---

## [1.0.20] - 2026-04-02

### Fixed
- WebGL: `postMessage` callbacks from unrelated windows no longer incorrectly complete an auth or purchase flow.

---

## [1.0.19] - 2026-04-01

### Fixed
- `WebAssembly.Table` crash in WebGL builds.

---

## [1.0.18] - 2026-03-26

### Fixed
- Multiple rounds of `WebAssembly.Table` fixes in WebGL (jslib popup handler rewritten).

---

## [1.0.17] - 2026-03-26

### Fixed
- `LinkGuestAsync` no longer attempts re-linking for users who are already on a full OpenID account.

---

## [1.0.16] - 2026-03-25

### Fixed
- `WebAssembly.Table` crash in WebGL builds.

---

## [1.0.15] - 2026-03-13

### Added
- Initial age verification endpoint integration.

---

## [1.0.14] - 2026-03-10

### Fixed
- Project name was not substituted in some API URLs, sending the literal `{project_name}` string.

---

## [1.0.13] - 2026-02-27

### Added
- Popup-blocked events — separate callbacks for when the browser blocks the auth or payment popup.

---

## [1.0.12] - 2026-02-10

### Added
- Automatic silent re-login when the access token expires — no browser interaction required.
- Concurrent `EnsureValidTokenAsync` calls are now safe — parallel callers wait for the first one to finish.
- Guest users are automatically logged in before a purchase if no token is present.

---

## [1.0.11] - 2026-02-05

### Added
- Optional attribution context (e.g. campaign, source) can be passed to the constructor and is automatically appended to all attribution events.

### Removed
- Auto-firing of attribution events on login — tracking is now manual only.

---

## [1.0.9] - 2026-02-03

### Fixed
- TextMeshPro dependency version compatibility.

---

## [1.0.8] - 2026-02-02

### Fixed
- Auth window was opening as a sized popup — now opens as a normal browser tab.

---

## [1.0.7] - 2026-01-29

### Fixed
- Payment window was opening as a sized popup — now opens as a normal browser tab.

---

## [1.0.6] - 2026-01-29

### Added
- `transaction_id` is now returned in the purchase result.

### Fixed
- A response from a previous purchase could accidentally resolve an ongoing one.

---

## [1.0.5] - 2026-01-29

### Fixed
- Payment cancellation and timeout now work correctly.

---

## [1.0.4] - 2026-01-26

### Added
- Payment result is now parsed from the `postMessage` JSON response.

### Fixed
- Unstable purchase flow in some cancellation scenarios.

---

## [1.0.3] - 2026-01-23

### Added
- **WebGL payments** — purchase flow via browser popup with result delivered through `postMessage` or deep link.

---

## [1.0.2] - 2026-01-23

### Fixed
- Starting a new login or purchase while one is already pending now correctly cancels the previous flow instead of being ignored.

---

## [1.0.1] - 2026-01-19

### Changed
- **BaseUrl** for Sandbox replaced with dev-main instead of dev-payments (Attribution and Payment Systems)

## [1.0.0] - 2026-01-16

### Added
- **Initial Public Release** of the Enjoylix SDK.
- **Authentication System:**
    - Guest Login support.
    - OpenID (Email) Login via external browser.
    - Guest-to-User account linking.
    - Persistent session storage (PlayerPrefs).
- **Payment System:**
    - `PurchaseClient` for handling in-app purchases (virtual currency).
    - Automatic login escalation: triggers OpenID login flow if a guest tries to purchase.
    - Purchase result handling via DeepLinks and polling.
- **Attribution:**
    - Automatic event tracking for `login` and `registration`.
    - Manual event tracking for `tutorial:complete` and `purchase`.
    - S2S (Server-to-Server) attribution support context.
- **Platform Support:**
    - **Mobile (Android/iOS):** Full DeepLink support.
    - **WebGL:** Support for `post_message` communication.
- **Editor Tools:**
    - **Deep Link Simulator:** New window (`Enjoylix > Debug > Deep Link Simulator`) to test Auth and Payment flows inside Unity Editor.
- **Samples:**
    - Added `EnjoylixDemo` sample scene demonstrating Auth, Linking, Payments, and Attribution.
