
# Enjoylix Unity SDK

Unity SDK for integrating **Enjoylix OpenID authentication**, **Payments**, **Age Verification**, and **Attribution** into Unity projects.

---

> 💡Tip: You can refer to our [GitBook](https://enjoylix.gitbook.io/docs) to get context of front-end and backend-end functions with Client side (Unity SDK)

---

# Quick Start

### 1. Install the SDK

Add the SDK to your `manifest.json` as a git dependency:

```json
{
  "dependencies": {
    "com.enjoylix.sdk": "https://github.com/enjoylix/unity-enjoylix-sdk.git#<tag>"
  }
}
```

Replace `<tag>` with any existing release tag from the repository's tags (e.g. `2.0.16`).

Alternatively, in Unity: `Window → Package Manager → + → Add package from git URL…` and paste `https://github.com/enjoylix/unity-enjoylix-sdk.git#<tag>`.

> Unity needs a `git` executable available in `PATH`. The repository is public, no credentials are required.

---

### 2. Create Config

```
Assets → Create → Enjoylix → EnjoylixConfig
```

Fill fields:

- ProjectName — your agreed project name
- DeepLinkScheme (mobile)
- Partner — partner identifier agreed with Enjoylix
- CurrentEnvironment (Dev / Prod)
- DevEnvironment / ProdEnvironment — references to `EnjoylixEnvironment` assets
- StorageEncryptionSalt — unique random string that encrypts the stored refresh token; set it once and never change it in a shipped game, or every player is logged out
- WebPortalMode (WebGL) — see [WebGL Portal Auto-Login](#webgl-portal-auto-login)

Then create one environment asset per environment:

```
Assets → Create → Enjoylix → EnjoylixEnvironment
```

- BaseServiceUrl
- AttributionServiceUrl
- RequestSignSecret (per environment; replace the placeholder with the key issued by the Enjoylix team — it signs the purchase request)

---

### 3. Initialize SDK

```csharp
public class EnjoylixBootstrap : MonoBehaviour
{
    [SerializeField] private EnjoylixConfig config;

    public static EnjoylixSdk Sdk;

    private void Awake()
    {
        Sdk = new EnjoylixSdk(config, "ENJOYLIX_ANDROID"); // the platform string agreed with Enjoylix
        Sdk.Initialize(); // restores saved session from PlayerPrefs
    }
}
```

`Initialize()` is synchronous and makes no network calls — it only loads a persisted session. It does not log the player in; call [`LoginOnStartAsync`](#login-on-startup-recommended) or another login method afterwards.

The `platform` parameter is required. It is an arbitrary string agreed with the Enjoylix team, used by age verification and reported as the `platform` of every attribution event — see [Platform Filtering](#platform-filtering).

You can also pass optional additional attribution context:

```csharp
var additionalContext = new Dictionary<string, string>
{
    { "campaign", "spring_sale" },
    { "source", "discord" }
};

Sdk = new EnjoylixSdk(config, Application.platform.ToString(), additionalContext);
```

These values are automatically attached to guest login and guest → OpenID linking attribution events. Useful for passing marketing or tracking parameters received from deep links, ads, or partner integrations.

---

# Authentication

## Login on Startup (recommended)

One call that covers every entry path: it restores a stored session if one is still usable, and otherwise hands off to the browser. Inside the Enjoylix web portal it routes to [`LoginOnStartWeb`](#webgl-portal-auto-login) automatically, so you do not need to branch on the platform yourself.

```csharp
string deviceId = SystemInfo.deviceUniqueIdentifier;

var session = await EnjoylixBootstrap.Sdk.LoginOnStartAsync(deviceId);
```

---

## Guest Login

```csharp
string deviceId = SystemInfo.deviceUniqueIdentifier;

await EnjoylixBootstrap.Sdk.LoginGuestAsync(deviceId);
```

---

## OpenID Login

```csharp
var session = await EnjoylixBootstrap.Sdk.LoginOpenIdAsync();
```

---

## Link Guest → OpenID

```csharp
string deviceId = SystemInfo.deviceUniqueIdentifier;

var session = await EnjoylixBootstrap.Sdk.LinkGuestAsync(deviceId);
```

---

## Login with Existing Token

```csharp
await EnjoylixBootstrap.Sdk.PerformLoginWithTokenAsync(accessToken);
```

---

## WebGL Portal Auto-Login

When the game runs inside the Enjoylix portal as a WebGL iframe, the portal handles authentication on behalf of the game. Call `LoginOnStartWeb` once on startup — it sends a `postMessage` to the parent portal window, receives an auth code, and exchanges it for a session via PKCE.

```csharp
// Call this instead of LoginGuestAsync / LoginOpenIdAsync when running inside the Enjoylix portal
await EnjoylixBootstrap.Sdk.LoginOnStartWeb();
```

The SDK decides whether it runs on the portal according to `WebPortalMode` in `EnjoylixConfig`:

| Mode | Behaviour |
|------|-----------|
| `Auto` (default) | Portal when the page is embedded in an iframe (`window.self !== window.top`), standalone otherwise. |
| `Portal` | Always portal. |
| `Standalone` | Never portal. Use this if you embed the WebGL build in an iframe on your own site — otherwise `LoginOnStartAsync` waits for a portal answer that never comes. |

The mode only matters in WebGL player builds; the Editor and native platforms are never "portal". The resolved value is exposed as `EnjoylixBootstrap.Sdk.IsWebPortal`, so the game can branch its own UI on it. On the portal the SDK keeps the session in memory for the duration of the page — no `PlayerPrefs` are written. `LoginOnStartAsync` picks `LoginOnStartWeb` on the portal and the guest/OpenID flow everywhere else, so you can call it unconditionally.

> Detection does not rely on `document.referrer` — browsers strip it in private/incognito modes and under a restrictive `Referrer-Policy`.

> This method is WebGL-only. Calling it in the Editor or on a native platform will throw `NotSupportedException`.

---

## Logout

```csharp
EnjoylixBootstrap.Sdk.Logout();
```

Clears the session and stored tokens, and stops the session ping. The device id is kept, so a later guest login returns the same guest.

---

## Keeping the Token Valid

Call `EnsureValidTokenAsync()` before any request of your own that carries the token. The check is local — a valid token costs no network round trip — and an expired one is renewed with the refresh token.

```csharp
var session = await EnjoylixBootstrap.Sdk.EnsureValidTokenAsync();
// session.accessToken is now guaranteed fresh
```

`PurchaseCreateAsync`, `IsAgeVerificationNeededAsync` and `StartAgeVerificationAsync` call it internally.

When a full (OpenID) session can no longer be renewed, `EnsureValidTokenAsync()` clears the stored tokens and throws `InvalidOperationException`. The SDK does not reopen the browser on its own — catch it and show your own login screen. Guest sessions re-login automatically.

---

# Payments

Payments are processed through a browser flow similar to authentication. Purchases are available to all users, including guests.

---

## Create Purchase

```csharp
using EnjoylixSDK.Purchase.Models;

var request = new PurchaseCreateRequest
{
    coin_amount = 100,
    game_name = "my_game_project",
    transaction_id = Guid.NewGuid().ToString("N"),
    transaction_name = "Gold Pack Small",
    transaction_image_url = "https://mysite.com/gold.png"
};

PaymentResult result = await EnjoylixBootstrap.Sdk.PurchaseCreateAsync(
    request,
    TimeSpan.FromMinutes(5)
);
```

Handle result:

```csharp
if (result == null)
{
    // The browser blocked the payment window — OnPopupBlocked has fired
    return;
}

if (result.success)
{
    Debug.Log($"Purchase successful! Payment ID: {result.payment_id}");
}
else
{
    Debug.LogError($"Purchase failed: {result.error}");
}
```

### Handling a Dismissed Purchase Flow

If the user closes the browser and returns to the app without completing the payment, the SDK automatically throws `UserDismissedException`. Catch it to handle cancellation gracefully:

```csharp
try
{
    var result = await EnjoylixBootstrap.Sdk.PurchaseCreateAsync(request, TimeSpan.FromMinutes(5));
}
catch (UserDismissedException)
{
    Debug.Log("User returned without completing the purchase.");
}
```

---

# Age Verification

Age verification is processed via a browser flow, similar to authentication and purchase flows.

**Prerequisite:** the user must be authenticated before calling either method. Both `IsAgeVerificationNeededAsync` and `StartAgeVerificationAsync` call `EnsureValidTokenAsync` internally — if there is no active session, they will throw `EnjoylixApiException` (HTTP 401). Call one of the login methods first.

---

## Typical Integration Pattern

```csharp
bool needed = await EnjoylixBootstrap.Sdk.IsAgeVerificationNeededAsync();

if (!needed)
    return; // already verified or not required for this user

// Show your own UI explaining what is about to happen,
// then invoke StartAgeVerificationAsync from a direct user action (e.g. button press).

try
{
    bool verified = await EnjoylixBootstrap.Sdk.StartAgeVerificationAsync(TimeSpan.FromMinutes(5));

    if (verified)
        Debug.Log("Age verification completed.");
    else
        Debug.LogWarning("User did not complete age verification.");
}
catch (UserDismissedException)
{
    // User closed the browser without completing verification (mobile, Editor, and WebGL)
}
catch (OperationCanceledException)
{
    // Timed out or CancellationToken was cancelled
}
```

> **Note:** `StartAgeVerificationAsync` must be invoked from a direct user action (button press). On WebGL and Safari, opening a browser window without a user gesture causes the popup to be blocked.

---

## Check if Age Verification is Required

```csharp
bool needed = await EnjoylixBootstrap.Sdk.IsAgeVerificationNeededAsync();
```

Returns `true` if the current user still needs to complete age verification, `false` if already verified or not required.

### Platform Filtering

The SDK sends the `platform` value (passed in the `EnjoylixSdk` constructor) with every `check_needed` request. The Enjoylix backend is configured per-project to require age verification only on specific platforms. If the current platform does not require verification, the backend returns `false` and no browser flow is triggered.

This means no client-side platform checks are needed — calling `IsAgeVerificationNeededAsync()` is always safe regardless of platform:

```csharp
Sdk = new EnjoylixSdk(config, Application.platform.ToString());

bool needed = await Sdk.IsAgeVerificationNeededAsync(); // false on platforms not requiring verification
```

The `platform` parameter is an arbitrary string — it does not have to be `Application.platform.ToString()`. You can pass any value that the backend is configured to recognize. This is useful when the same Unity build runs in different deployment contexts that require different verification rules. For example, a WebGL build deployed on the Enjoylix portal and the same build embedded on your own site can be distinguished by passing different platform strings:

```csharp
// On the Enjoylix portal — verification required
Sdk = new EnjoylixSdk(config, "web_enjoylix");

// On your own site — verification not required
Sdk = new EnjoylixSdk(config, "web_own_site");
```

To enable or disable age verification for a specific platform string, configure it in the Enjoylix project settings on the backend.

---

## Start Age Verification

```csharp
bool verified = await EnjoylixBootstrap.Sdk.StartAgeVerificationAsync(TimeSpan.FromMinutes(5));

if (verified)
    Debug.Log("Age verification completed.");
else
    Debug.LogWarning("Age verification did not complete.");
```

Opens the age verification page in the browser and waits for the result via deep link (Android / iOS / Editor) or `postMessage` (WebGL).

`false` is returned when:
- the user fails or declines verification on the external page
- the popup was blocked by the browser (WebGL) — `OnPopupBlocked` is also fired in this case

### Handling a Dismissed Verification Flow

If the user closes the browser without completing verification, the SDK throws `UserDismissedException`. This applies to mobile, Editor, and WebGL (when the popup window is closed before the result arrives):

```csharp
try
{
    bool verified = await EnjoylixBootstrap.Sdk.StartAgeVerificationAsync(TimeSpan.FromMinutes(5));
    // false = user reached the page but did not complete verification,
    //         or popup was blocked (WebGL)
}
catch (UserDismissedException)
{
    // User closed the browser without completing verification
}
catch (OperationCanceledException)
{
    // Timed out or CancellationToken was cancelled
}
```

---

# Attribution

The SDK automatically sends only one event:

- ping — session heartbeat, starts once the game has called `SetPlayerId()` on an authenticated session (any entry path) and then repeats every 6 hours while the game is running

`login`, `registration`, `tutorial:complete` and purchase events are **not** sent automatically. Call `TrackLogin()`, `TrackRegistration()`, `TrackTutorialComplete()` and `TrackPurchase()` yourself for the client-side fallback — see [Manual tracking](#manual-tracking).

## Launch query parameters

The portal passes its own query string on to the game url, so the params the game was launched with tell the backend which campaign the player arrived from. The SDK reads them from `Application.absoluteURL` when it is constructed and sends them in two places — the `query_params` of attribution events, and the query string of the token request:

```
launch url    https://enjoylix.com/ac/?utm_source=facebook&utm_medium=cpc&utm_campaign=summer_sale
token request POST /api/v2/auth/ac/token?utm_source=facebook&utm_medium=cpc&utm_campaign=summer_sale
```

This is what keeps an in-game registration attributed to its campaign instead of being counted as organic. It needs no integration code and applies to WebGL builds; on mobile and in the Editor the launch url is empty and nothing is appended. If your game resolves its launch url on its own, hand it over and both the token request and the attribution events will use its query string:

```csharp
EnjoylixBootstrap.Sdk.UpdateAttributionContext(new AttributionContext { RawDeeplinkUrl = launchUrl });
```

## `partner` and `player_id`

Every attribution event carries a `partner` and a `player_id`. These are purely client-side fields — the SDK never derives them from the launch deep link or anywhere else.

**`partner`** — the identifier agreed with Enjoylix at the integration stage. It is a build-time constant, so set it once in the `EnjoylixConfig` asset (`Partner` field). No integration code needed.

**`player_id`** — your in-game profile id. The game only knows it after it resolves its own profile, so pass it at runtime:

```csharp
EnjoylixBootstrap.Sdk.SetPlayerId(profile.Id);
```

If a field is never set, an empty string is sent. The values persist until changed and are attached to every event sent afterwards. `null` or an empty string is ignored and keeps the previous value.

`SetPartner()` also exists if a build needs to override the config value at runtime.

The player id only exists after the player has authenticated through Enjoylix, so the chain is:

```
Enjoylix auth  →  game resolves its profile  →  SetPlayerId()  →  first ping
```

The session `ping` waits for `SetPlayerId()` and is sent on that call — nothing goes out before it, since a `ping` without `player_id` is not usable on the backend. After that it repeats every 6 hours on its own.

## Manual tracking

```csharp
EnjoylixBootstrap.Sdk.TrackLogin();
EnjoylixBootstrap.Sdk.TrackRegistration();
EnjoylixBootstrap.Sdk.TrackTutorialComplete();

EnjoylixBootstrap.Sdk.TrackPurchase(
    "purchase:complete",
    "google_play",
    "checkout_1",
    "tx_123",
    4.99f,
    "USD",
    false
);
```

---

# Error Handling

The SDK throws standard exceptions. Wrap async calls in `try/catch`.

```csharp
try
{
    var session = await EnjoylixBootstrap.Sdk.LoginOpenIdAsync();
}
catch (UserDismissedException)
{
    // User returned to the app without completing the flow
}
catch (FlowTimeoutException e)
{
    // The browser flow returned no result in time (e.Context, e.Timeout)
}
catch (OperationCanceledException)
{
    // CancellationToken was triggered
}
catch (EnjoylixApiException e)
{
    // Also catches EnjoylixNetworkException (transient failure after all retries)
    Debug.LogError($"API error {e.StatusCode}: {e.Message}");
}
catch (Exception e)
{
    Debug.LogException(e);
}
```

| Exception | Raised when |
|---|---|
| `UserDismissedException` | The player closed the browser without finishing a purchase or verification. For purchases the SDK has already confirmed with the server that no payment completed. |
| `UserCancelledException` | WebGL only — the player declined your `EnjoylixConfirmOpen` dialog, so the window was never opened. See [Confirmation before opening](#confirmation-before-opening-safari). |
| `FlowTimeoutException` | A browser flow returned no result in time — 5 minutes for `LoginOpenIdAsync` / `LinkGuestAsync` / `LoginOnStartAsync`, 2 minutes for `LoginOnStartWeb`. |
| `EnjoylixNetworkException` | A request ran out of retries for a transient reason. Derives from `EnjoylixApiException`; `Attempts` says how many were made. |
| `EnjoylixApiException` | The backend answered with an error status. `StatusCode`, `ResponseBody` and `ValidationError` carry the detail. |
| `InvalidOperationException` | A full (OpenID) session can no longer be renewed. Stored tokens have already been cleared. |

Transient network failures (connection errors, timeouts, `429`, `5xx`) on guest login, `me` and the token request are retried up to three times inside the SDK. A `401` on `PurchaseCreateAsync` or `IsAgeVerificationNeededAsync` triggers one silent session refresh and a single repeat.

## Popup Blocking

Some browsers block new windows when they are not triggered directly by a user interaction (e.g. Safari on macOS).

The SDK exposes a single unified event on `EnjoylixSdk`:

```csharp
EnjoylixBootstrap.Sdk.OnPopupBlocked += url =>
{
    Debug.LogWarning($"Popup was blocked: {url}");
    // Notify the user to allow popups for this site
};
```

This event fires for all popup-using flows: authentication, purchase, and age verification. The call itself does not throw: `LoginOpenIdAsync`, `LinkGuestAsync`, `LoginOnStartAsync` and `PurchaseCreateAsync` return `null`, and `StartAgeVerificationAsync` returns `false`.

Note: Popup blocking is most common on Safari (macOS / iOS) and when the action is not triggered directly from a user click.

### Confirmation before opening (Safari)

Safari only allows `window.open` inside a DOM click handler. A click on a Unity UI button does not count, and the purchase URL is only known after a network request, so Safari blocks the popup almost every time.

To handle this, define `window.EnjoylixConfirmOpen` on the game page (WebGL template or your own `.jspre`). When `window.open` is blocked, the SDK calls it instead of failing right away:

```js
// request: { url, windowName } — windowName is "enjoylix_pay", "enjoylix_auth" or "enjoylix_age_verification"
window.EnjoylixConfirmOpen = function (request, proceed, cancel) {
    myOverlay.show(request);
    myOverlay.onConfirm = function () { myOverlay.hide(); proceed(); }; // synchronously inside the click handler
    myOverlay.onCancel  = function () { myOverlay.hide(); cancel(); };
};
```

- The confirmation UI must be HTML: `proceed()` has to run synchronously in a DOM click handler. Calling it after `await`, `setTimeout` or from Unity will be blocked again.
- `proceed()` opens the window and the flow continues as usual. `cancel()` ends it with `UserCancelledException` — the window was never opened, so for purchases no payment status check is made (unlike `UserDismissedException`).
- Both are one-shot and are ignored once the flow has ended (timeout, cancellation).
- If the hook is not defined, throws, or the window is still blocked after `proceed()`, the flow fails with `PopupBlockedException` and `OnPopupBlocked` fires, as before.
- Browsers that don't block the popup never call the hook.

---

# Deep Links

## Android Setup

Add to `AndroidManifest.xml`

```xml
<intent-filter>
    <action android:name="android.intent.action.VIEW"/>

    <category android:name="android.intent.category.DEFAULT"/>
    <category android:name="android.intent.category.BROWSABLE"/>

    <data
        android:scheme="enjoylix"
        android:host="com.enjoylix.sdk"
        android:pathPrefix="/path"/>
</intent-filter>
```

---

## iOS Setup

Unity:

```
Edit → Project Settings → Player → iOS
```

Add URL scheme:

```
enjoylix
```

---

# Editor DeepLink Simulator

Unity Editor cannot intercept custom URL schemes.

Use:

```
Enjoylix → Debug → Deep Link Simulator
```

Steps:

1. Enter Play Mode
2. Trigger a login / purchase / age verification flow
3. Complete the action in the browser
4. Copy the redirect URL
5. Paste into the simulator
6. Click **Simulate Incoming DeepLink**

Example redirect:

```
enjoylix://com.enjoylix.sdk/path?token=...
```

---

# Platform Behavior

| Platform | Return Mechanism |
|---|---|
| Android | Deep link |
| iOS | Deep link |
| WebGL | post_message |
| Editor | Deep Link Simulator |

---

# Auth HTTP Status Codes

### `/guest_login`

- **409** — Device ID already registered as a non-guest
- **429** — Too many guest accounts from this IP

### `/register`

- **400** — User with this email already exists

### `/link_guest`

- **400** — User with this email already exists, or guest already linked to another account
- **404** — User not found

### Common

- **401** — Invalid or expired credentials
- **422** — Validation error
