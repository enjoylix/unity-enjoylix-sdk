
# Enjoylix Unity SDK

Unity SDK for integrating **Enjoylix OpenID authentication**, **Payments**, **Age Verification**, and **Attribution** into Unity projects.

---

> 💡Tip: You can refer to our [GitBook](https://enjoylix.gitbook.io/docs) to get context of front-end and backend-end functions with Client side (Unity SDK)

---

# Quick Start

### 1. Install the SDK

Add the registry and dependency to `manifest.json`.

```json
{
  "scopedRegistries": [
    {
      "name": "PublicNPMRegistry",
      "url": "https://nexus.playful-fairies.com/repository/npm-unity-public",
      "scopes": ["com.enjoylix"],
      "overrideBuiltIns": true
    }
  ],
  "dependencies": {
    "com.enjoylix.sdk": "2.0.0"
  }
}
```

---

### 2. Configure Registry Authentication

Create `.upmconfig.toml`

**Windows**

```
%USERPROFILE%\.upmconfig.toml
```

**macOS / Linux**

```
~/.upmconfig.toml
```

Example:

```toml
[npmAuth."https://nexus.playful-fairies.com/repository/npm-unity-public"]
_auth = "YOUR_TOKEN"
alwaysAuth = true
```

---

### 3. Create Config

```
Assets → Create → Enjoylix → EnjoylixConfig
```

Fill fields:

- ProjectName
- Environment (Dev / Prod)
- DeepLinkScheme (mobile)
- BaseServiceUrl
- AttributionServiceUrl
- RequestSignSecret (per environment; replace the placeholder with the key issued by the Enjoylix team — it signs the purchase request)

---

### 4. Initialize SDK

```csharp
public class EnjoylixBootstrap : MonoBehaviour
{
    [SerializeField] private EnjoylixConfig config;

    public static EnjoylixSdk Sdk;

    private void Awake()
    {
        Sdk = new EnjoylixSdk(config, Application.platform.ToString());
        Sdk.Initialize(); // restores saved session from PlayerPrefs
    }
}
```

The `platform` parameter is required. It is used by age verification and attribution to identify the runtime environment.

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

---

# Payments

Payments are processed through a browser flow similar to authentication.

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

SDK automatically tracks:

- login
- registration
- ping — session heartbeat, starts once the game has called `SetPlayerId()` on an authenticated session (any entry path) and then repeats every 6 hours while the game is running
- purchases
- tutorial completion

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

Manual tracking:

```csharp
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

This event fires for all popup-using flows: authentication, purchase, and age verification.

Note: Popup blocking is most common on Safari (macOS / iOS) and when the action is not triggered directly from a user click.

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
