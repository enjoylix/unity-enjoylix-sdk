# Enjoylix Unity SDK — API Reference

---

> 💡Tip: You can refer to our [GitBook](https://enjoylix.gitbook.io/docs) to get context of front-end and backend-end functions with Client side (Unity SDK)

---

## EnjoylixSdk

Main SDK entry point. All features are exposed directly through this class.

```csharp
public class EnjoylixSdk
```

### Constructor

```csharp
EnjoylixSdk(EnjoylixConfig config, string platform, Dictionary<string, string> additionalAttributionContext = null)
```

| Parameter | Type | Description |
| :---- | :---- | :---- |
| `config` | `EnjoylixConfig` | ScriptableObject with project/environment settings |
| `platform` | `string` | Runtime platform string (e.g. `"ANDROID"`, `"IOS"`, `"WEBGL"`, `"UNITY_EDITOR"`) |
| `additionalAttributionContext` | `Dictionary<string, string>` | Optional extra key-value pairs attached to attribution events |

---

### Properties

| Name | Type | Description |
| :---- | :---- | :---- |
| `IsEmailLogin` | `bool` | `true` if the current session is a full OpenID (non-guest) login |
| `IsKnownFullAccount` | `bool` | `true` if the persisted `is_guest` flag is `false` — the device was previously linked to a full account. Available before a session is established. |
| `EnjoylixId` | `string` | User ID from the Enjoylix backend (`null` if not logged in) |
| `AccessToken` | `string` | Current JWT access token (`null` if not logged in) |

---

### Events

```csharp
event Action<string> OnPopupBlocked
```

Fired when a browser window is blocked by the OS or browser (WebGL popup, mobile deeplink). The string argument is the URL that could not be opened.

```csharp
event Action<LoginKind, bool> OnLoginCompleted
```

Fired after any login attempt. Second argument is `true` on success, `false` on failure.

---

### Initialization & Session

```csharp
void Initialize()
```

Restores the saved session from `PlayerPrefs` (device ID, guest ID, access token). Call once on startup before any other SDK method.

```csharp
void Logout()
```

Clears the current session (token, user info) and removes persisted data.

```csharp
Task<AuthSession> EnsureValidTokenAsync(CancellationToken ct = default)
```

Validates the current token against the server. For guest sessions, re-authenticates automatically. For OpenID sessions, clears stored tokens and throws `InvalidOperationException` if expired — the caller must handle this and prompt the user to log in again. Serializes concurrent calls — safe to call in parallel.

---

### Authentication Methods

```csharp
Task<string> LoginGuestAsync(string deviceId)
```

Logs in as a guest using the provided device ID. Returns the guest user ID.

---

```csharp
Task<AuthSession> LoginOpenIdAsync()
```

Opens the Enjoylix login page in the browser and waits for the user to complete the OpenID flow. Timeout is fixed at 5 minutes.

---

```csharp
Task<AuthSession> LinkGuestAsync(string deviceId)
```

Upgrades an existing guest session to a full OpenID account. Opens the registration page in the browser. If the user is already on a full account, returns immediately. Timeout is fixed at 5 minutes.

---

```csharp
Task<AuthSession> PerformLoginWithTokenAsync(string token, CancellationToken ct = default)
```

Logs in using an existing access token (e.g. received externally).

---

```csharp
Task<AuthSession> LoginOnStartWeb(CancellationToken ct = default)
```

WebGL portal only. Sends a `postMessage` to the parent portal window with a PKCE `code_challenge`, waits for the portal to reply with an auth code, then exchanges the code for a `TokensPair`. Default timeout is 2 minutes.

Throws `NotSupportedException` on non-WebGL platforms.

---

### Purchase Methods

```csharp
Task<PaymentResult> PurchaseCreateAsync(
    PurchaseCreateRequest req,
    TimeSpan timeout,
    CancellationToken ct = default)
```

Ensures a valid token, creates a transaction, opens the payment UI in the browser, and waits for the result via deep link or post message.

---

### Age Verification Methods

**Prerequisite:** an active session is required. Both methods call `EnsureValidTokenAsync` internally and throw `EnjoylixApiException` (401) if no session exists. Call a login method before using these.

```csharp
Task<bool> IsAgeVerificationNeededAsync(CancellationToken ct = default)
```

Returns `true` if the current user still needs to complete age verification, `false` if already verified or not required. The response is platform-aware — the backend uses the `platform` value passed to the `EnjoylixSdk` constructor to determine whether verification is required on the current platform.

---

```csharp
Task<bool> StartAgeVerificationAsync(TimeSpan timeout, CancellationToken ct = default)
```

Opens the age verification page in the browser and waits for the result via deep link (Android / iOS / Editor) or `postMessage` (WebGL).

Returns `true` if verification completed successfully.

Returns `false` when:
- the user fails or declines verification on the external page
- the popup was blocked by the browser (WebGL) — `OnPopupBlocked` also fires in this case

Throws `UserDismissedException` if the user closes the browser without completing verification — on mobile, Editor, and WebGL (popup closed before result arrives).

Throws `OperationCanceledException` on timeout or token cancellation.

> `PopupBlockedException` is handled internally — it is never thrown from this method.

> Must be called from a direct user action (e.g. button press). On WebGL and Safari, opening a popup without a user gesture causes the browser to block it.

---

### Attribution Methods

```csharp
void TrackLogin()
void TrackRegistration()
void TrackTutorialComplete()
```

#### `partner` and `player_id`

Both are purely client-side fields — the SDK never derives them from the launch deep link or any other source. Whatever the game provides is what is sent; if nothing is provided, an empty string goes out.

| Field | Value | Where it is set |
| :---- | :---- | :---- |
| `partner` | partner identifier agreed with Enjoylix at the integration stage | `Partner` field on the `EnjoylixConfig` asset |
| `player_id` | your in-game profile id | `SetPlayerId()` at runtime, once the game knows it |

`partner` is a build-time constant, so it lives on the config asset and needs no integration code. `player_id` is only known after the player has authenticated through Enjoylix, so it is handed over at runtime:

```csharp
EnjoylixBootstrap.Sdk.SetPlayerId(profile.Id);
```

Both can also be changed at runtime, and `SetPartner()` overrides the config value if a build ever needs to:

```csharp
void SetPartner(string partner)
void SetPlayerId(string playerId)
```

Every attribution event (`login`, `registration`, `ping`, purchases, `tutorial:complete`) carries the values that are set at the moment it is sent. Values persist until changed; passing `null` or an empty string is ignored and keeps the previous value.

> `SetPlayerId()` is what releases the session `ping` — see below.


#### Session ping

The `ping` starts once the SDK has both identifiers, and then repeats every 6 hours while the game keeps running. The integration chain is:

```
Enjoylix auth  →  game resolves its profile  →  SetPlayerId()  →  first ping
```

`auth_id` is filled by the SDK when the session is applied. `player_id` can only come from the game, and only after the player has authenticated — so the `ping` waits for `SetPlayerId()` and is sent on that call. Nothing is sent before then: a `ping` without `player_id` is not usable on the backend.

Order does not matter — whichever identifier arrives last starts the schedule. Setting `player_id` before a session exists is fine too; the `ping` then goes out when the session is applied.

Every entry path is covered, because `auth_id` comes from the single point where a session is applied — right after the SDK has confirmed the account with `GET /me`:

| Entry | Path |
| :---- | :---- |
| explicit login | guest login, OpenID login, guest linking, token login, WebGL portal login |
| silent restore | stored access token still valid, confirmed by `/me` |
| silent re-login | access token expired, refreshed by refresh token, confirmed by `/me` |

- Repeated `EnsureValidTokenAsync` calls and token refreshes do not reset the interval — the schedule restarts only when the account changes or after `Logout()`, which stops it.
- The interval is real (wall-clock) time and keeps counting while the game is minimized or sitting in a background browser tab. On platforms where the OS fully suspends a backgrounded app, the request itself cannot leave the device until the app is resumed — in that case the due `ping` is sent immediately on resume and the next one is scheduled from that moment.
- `TrackLogin()` also makes sure the schedule is running; if it is already running for this account, no duplicate `ping` is sent.
- If the game never calls `SetPlayerId()`, no `ping` is ever sent.

```json
{
  "event": "ping",
  "timestamp": 1726992000,
  "player_id": "1234567890",
  "auth_id": "28c58c502bad4733bac39d4a993b6616",
  "partner": "your_partner_name",
  "platform": "website",
  "event_data": {}
}
```

```csharp
void TrackPurchase(
    string eventType,
    string source,
    string checkoutId,
    string transactionId,
    float amount,
    string currencyCode,
    bool isQaPurchase)
```

```csharp
void UpdateAttributionContext(AttributionContext ctx)
```

---

## Networking

Every HTTP request the SDK issues — authentication, purchase, age verification, and attribution — automatically carries an `X-Caller-Identity` header so the backend can identify the client and its version:

```
X-Caller-Identity: unity-sdk/<version>
```

`<version>` is the installed SDK package version (e.g. `2.0.8`). When a real version cannot be resolved — for example a local development checkout — the value falls back to `unity-sdk/dev`.

The header is added automatically; no configuration is required. In standalone builds the version is baked into a `Resources` asset at build time (via an editor build processor that temporarily generates `Assets/EnjoylixSDK.Generated`, then removes it after the build); in the Editor it is read directly from the package manifest.

### Launch query parameters

The query string the game was launched with is forwarded to the token request:

```
launch url    https://enjoylix.com/ac/?utm_source=facebook&utm_medium=cpc&utm_campaign=summer_sale
token request POST /api/v2/auth/ac/token?utm_source=facebook&utm_medium=cpc&utm_campaign=summer_sale
```

The portal passes its own query string on to the game url, so these params identify the campaign the player arrived from. The backend needs them on the token request to attribute the in-game registration it creates; without them the registration is counted as organic.

Every param is forwarded as-is — no allow-list and no filtering — and the same set is reported in the `query_params` of attribution events. The source is `Application.absoluteURL`, read when `EnjoylixSdk` is constructed, so this applies to WebGL builds; on mobile and in the Editor the launch url is empty and the token request is unchanged. A game that resolves its launch url itself can supply it with `UpdateAttributionContext(new AttributionContext { RawDeeplinkUrl = url })` — from that point its query string is used for both the token request and attribution events.

No other request carries these params: `/api/v2/auth/login` and `/api/v1/auth/token` are portal endpoints the SDK never calls.

---

## Payment Models

### PurchaseCreateRequest

```csharp
public sealed class PurchaseCreateRequest
{
    public int coin_amount;
    public string game_name;
    public string transaction_id;
    public string transaction_image_url;
    public string transaction_name;
}
```

### PaymentResult

```csharp
public sealed class PaymentResult
{
    public bool success;
    public string payment_id;
    public string transaction_id;
    public string error;
    public string raw;
}
```

---

## Auth Models

### AuthSession

```csharp
public class AuthSession
{
    public string accessToken;
    public OpenIdUserResponse me;
}
```

### OpenIdUserResponse

```csharp
public class OpenIdUserResponse
{
    public string id;
    public string email;
    public bool is_guest;
}
```

---

## AttributionContext

```csharp
public class AttributionContext
{
    public string Partner;
    public string PlayerId;
    public string AuthId;
    public string CountryCode;
    public string ClientIp;
    public string PlatformId;
    public string RawDeeplinkUrl;
}
```

---

## Enums

### LoginKind

```csharp
GuestLogin    // LoginGuestAsync completed
OpenIdLogin   // LoginOpenIdAsync completed
GuestLink     // LinkGuestAsync completed
TokenLogin    // PerformLoginWithTokenAsync completed
```

---

## Error Handling

The SDK throws standard exceptions. Wrap calls in `try/catch`.

```csharp
try
{
    var session = await sdk.LoginOpenIdAsync();
}
catch (UserDismissedException)
{
    // User returned to the app without completing the flow
}
catch (OperationCanceledException)
{
    // Timed out or CancellationToken was triggered
}
catch (EnjoylixApiException e)
{
    Debug.LogError($"API error {e.StatusCode}: {e.Message}");
}
catch (Exception e)
{
    Debug.LogException(e);
}
```

### Exception Types

| Type | When thrown |
| :---- | :---- |
| `UserDismissedException` | User closed the browser without completing the flow (mobile, Editor, and WebGL) |
| `PopupBlockedException` | Browser blocked the popup window before it could open (WebGL) |
| `OperationCanceledException` | Timeout elapsed or `CancellationToken` was canceled |
| `EnjoylixApiException` | HTTP error from the backend; exposes `StatusCode`, raw body, and optional `ValidationError` |

### OnPopupBlocked Event

```csharp
sdk.OnPopupBlocked += url =>
{
    Debug.LogWarning($"Popup blocked: {url}");
    // Show UI asking user to allow popups
};
```
