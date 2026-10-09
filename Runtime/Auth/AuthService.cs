using System;
using System.Threading;
using System.Threading.Tasks;
using EnjoylixSDK.Auth.Internal;
using EnjoylixSDK.Auth.Models;
using EnjoylixSDK.Config;
using EnjoylixSDK.Net;
using UnityEngine;

namespace EnjoylixSDK.Auth
{
    public sealed class AuthService
    {
        private const string LOG_TAG = nameof(AuthService);

        public event Action<string> OnTokenReceived;
        public event Action OnSessionApplied;
        public event Action<Exception> OnAuthError;
        public event Action<string> OnPopupBlocked;
        public event Action<LoginKind, bool> OnLoginCompleted;
        public event Action<ValidationErrorResponse> OnValidationError;

        public bool IsEmailLogin => Me != null && !Me.is_guest;
        public bool CanMakePurchases => true;
        public bool IsKnownNonGuest => _storage.LoadIsGuest() == false;

        public string GuestId { get; private set; }
        public string AccessToken { get; private set; }
        public string DeviceId { get; private set; }
        public OpenIdUserResponse Me { get; private set; }

        private readonly UrlBuilder _urlBuilder;
        private readonly AuthApiClient _api;
        private readonly AuthFlow _flow;
        private readonly WebBrowserService _browser;
        private readonly IAuthStorage _storage;
        private readonly string _gameName;

        // Serializes concurrent EnsureValidToken calls (purchase, init, UI etc.)
        private readonly SemaphoreSlim _authGate = new SemaphoreSlim(1, 1);

        private string _sessionToken;
        private DateTime? _sessionExpiresUtc;

        private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(30);

        public AuthService(UrlBuilder urlBuilder, WebBrowserService webBrowserService, EnjoylixConfig config)
        {
            _urlBuilder = urlBuilder;
            _api = new AuthApiClient(_urlBuilder);
            _flow = new AuthFlow();
            _browser = webBrowserService;
            _storage = AuthStorageFactory.Create(config);
            _gameName = config.ProjectName;
            DeepLinkListener.Instance.RegisterHandler(_flow.HandleDeepLink);
        }

        // Loads saved ids/token into memory. Does NOT auto-login.
        public void Initialize()
        {
            DeviceId = _storage.LoadDeviceId();
            GuestId = _storage.LoadGuestId();

            var savedToken = _storage.LoadAccessToken();
            if (!string.IsNullOrEmpty(savedToken))
                AccessToken = savedToken;
        }

        public void Logout()
        {
            GuestId = null;
            AccessToken = null;
            Me = null;
            InvalidateSession();
            _storage.Clear();
        }

        // Call before any token-using request. Re-authenticates if token is missing or expired.
        public async Task<AuthSession> EnsureValidTokenAsync(CancellationToken ct = default)
        {
            await _authGate.WaitAsync(ct);
            try
            {
                if (HasUnexpiredSession())
                    return new AuthSession { accessToken = AccessToken, me = Me };

                if (string.IsNullOrEmpty(AccessToken))
                    return await TryRefreshOrReloginAsync(ct);

                try
                {
                    var me = await _api.GetMeAsync(AccessToken, ct);
                    ApplySession(AccessToken, me);
                    return new AuthSession { accessToken = AccessToken, me = Me };
                }
                catch (Exception ex) when (IsHttpStatus(ex, 401))
                {
                    return await TryRefreshOrReloginAsync(ct);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LOG_TAG} EnsureValidToken failed: {e.Message}");
                EmitValidationIfAny(e);
                OnAuthError?.Invoke(e);
                throw;
            }
            finally
            {
                _authGate.Release();
            }
        }

        public async Task<AuthSession> ForceRefreshAsync(CancellationToken ct = default)
        {
            await _authGate.WaitAsync(ct);
            try
            {
                InvalidateSession();
                return await TryRefreshOrReloginAsync(ct);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LOG_TAG} ForceRefresh failed: {e.Message}");
                EmitValidationIfAny(e);
                OnAuthError?.Invoke(e);
                throw;
            }
            finally
            {
                _authGate.Release();
            }
        }

        public async Task<string> LoginGuestAsync(string deviceId, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrEmpty(deviceId))
                    throw new ArgumentException("deviceId is empty", nameof(deviceId));

                DeviceId = deviceId;
                _storage.SaveDeviceId(deviceId);

                var resp = await _api.GuestLoginAsync(deviceId, ct);

                GuestId = resp?.id;
                if (!string.IsNullOrEmpty(GuestId))
                    _storage.SaveGuestId(GuestId);

                if (!string.IsNullOrEmpty(resp?.refresh_token))
                    _storage.SaveRefreshToken(resp.refresh_token);

                await ProcessAccessTokenAsync(resp?.access_token, ct);

                OnLoginCompleted?.Invoke(LoginKind.GuestLogin, !string.IsNullOrEmpty(GuestId));
                return GuestId;
            }
            catch (Exception e)
            {
                if (IsHttpStatus(e, 409))
                {
                    // Device is linked to a full account — persist so ReLoginByPolicyAsync doesn't retry guest login.
                    _storage.SaveIsGuest(false);
                    ClearStoredTokens();
                }
                EmitValidationIfAny(e);
                OnAuthError?.Invoke(e);
                OnLoginCompleted?.Invoke(LoginKind.GuestLogin, false);
                throw;
            }
        }

        public async Task<AuthSession> LoginOpenIdAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            try
            {
                var session = await LoginOpenIdCoreAsync(timeout, ct);
                OnLoginCompleted?.Invoke(LoginKind.OpenIdLogin, true);
                return session;
            }
            catch (PopupBlockedException e)
            {
                OnPopupBlocked?.Invoke(e.Url);
                OnLoginCompleted?.Invoke(LoginKind.OpenIdLogin, false);
                return null;
            }
            catch (Exception e)
            {
                Debug.Log($"{LOG_TAG} OpenID login failed: {e.Message}");
                EmitValidationIfAny(e);
                OnAuthError?.Invoke(e);
                OnLoginCompleted?.Invoke(LoginKind.OpenIdLogin, false);
                throw;
            }
        }

        public async Task<AuthSession> LoginOnStartWeb(TimeSpan timeout, CancellationToken ct = default)
        {
            Debug.Log($"{LOG_TAG} LoginOnStartWeb called");
            try
            {
                var session = await LoginOnStartWebCore(timeout, ct);
                OnLoginCompleted?.Invoke(LoginKind.OpenIdLogin, true);

                return session;
            }
            catch (Exception e)
            {
                Debug.Log($"{LOG_TAG} OpenID login failed: {e.Message}");
                EmitValidationIfAny(e);
                OnAuthError?.Invoke(e);
                OnLoginCompleted?.Invoke(LoginKind.OpenIdLogin, false);
                throw;
            }
        }

        public async Task<AuthSession> LoginOnStartAsync(string deviceId, TimeSpan timeout, CancellationToken ct = default)
        {
            if (!string.IsNullOrEmpty(deviceId))
            {
                DeviceId = deviceId;
                _storage.SaveDeviceId(deviceId);
            }

            if (string.IsNullOrEmpty(DeviceId))
                DeviceId = _storage.LoadDeviceId();

            try
            {
                var restored = await TryRestoreSessionAsync(ct);
                if (restored != null)
                {
                    Debug.Log($"{LOG_TAG} Startup login restored a local session, browser not opened.");
                    OnLoginCompleted?.Invoke(LoginKind.StartupLogin, true);
                    return restored;
                }

                var session = await LoginOnStartNativeCoreAsync(timeout, ct);
                OnLoginCompleted?.Invoke(LoginKind.StartupLogin, true);
                return session;
            }
            catch (PopupBlockedException e)
            {
                OnPopupBlocked?.Invoke(e.Url);
                OnLoginCompleted?.Invoke(LoginKind.StartupLogin, false);
                return null;
            }
            catch (Exception e)
            {
                Debug.Log($"{LOG_TAG} Startup login failed: {e.Message}");
                EmitValidationIfAny(e);
                OnAuthError?.Invoke(e);
                OnLoginCompleted?.Invoke(LoginKind.StartupLogin, false);
                throw;
            }
        }

        public async Task<AuthSession> LinkGuestAsync(string deviceId, TimeSpan timeout, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("deviceId is empty", nameof(deviceId));

            DeviceId = deviceId;
            _storage.SaveDeviceId(deviceId);

            InvalidateSession();

            await EnsureValidTokenAsync(ct);
            if (IsEmailLogin)
            {
                Debug.LogWarning($"{LOG_TAG} LinkGuestAsync called for non-guest user. Skip linking.");
                OnLoginCompleted?.Invoke(LoginKind.OpenIdLogin, true);
                return new AuthSession { accessToken = AccessToken, me = Me };
            }

            try
            {
                string codeVerifier = PkceUtils.GenerateCodeVerifier();
                string codeChallenge = PkceUtils.GenerateCodeChallenge(codeVerifier);

                string url = _urlBuilder.BuildAuthPageUrl(isRegistration: true, codeChallenge: codeChallenge, token: AccessToken);
                var authCode = await _browser.RunFlowAsync(
                    url,
                    "enjoylix_auth",
                    json => _flow.HandlePostMessage(json),
                    (t, linkedCt) => _flow.WaitForResultAsync(t, linkedCt),
                    timeout,
                    ct);
                var session = await ProcessAuthCodeAsync(authCode, codeVerifier, ct);
                OnLoginCompleted?.Invoke(LoginKind.GuestLink, true);
                return session;
            }
            catch (PopupBlockedException e)
            {
                OnPopupBlocked?.Invoke(e.Url);
                OnLoginCompleted?.Invoke(LoginKind.GuestLink, false);
                return null;
            }
            catch (Exception e)
            {
                Debug.Log($"{LOG_TAG} Guest link failed: {e.Message}");
                EmitValidationIfAny(e);
                OnAuthError?.Invoke(e);
                OnLoginCompleted?.Invoke(LoginKind.GuestLink, false);
                throw;
            }
        }

        public async Task<AuthSession> TokenLoginAsync(string token, CancellationToken ct)
        {
            try
            {
                var session = await ProcessAccessTokenAsync(token, ct);
                OnLoginCompleted?.Invoke(LoginKind.TokenLogin, true);
                return session;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LOG_TAG} Token login failed: {e.Message}");
                EmitValidationIfAny(e);
                OnAuthError?.Invoke(e);
                OnLoginCompleted?.Invoke(LoginKind.TokenLogin, false);
                throw;
            }
        }

        // One-time ticket proving to the game backend that this user is authenticated here.
        // The server expires it after ~60s and burns it on first validation, so request it
        // immediately before the call that consumes it — never cache or reuse the value.
        // If the backend rejects a ticket, call this again to mint a fresh one.
        public async Task<string> CreateGameSessionTicketAsync(CancellationToken ct = default)
        {
            await EnsureValidTokenAsync(ct);

            try
            {
                var session = await _api.CreateGameSessionAsync(AccessToken, _gameName, ct);
                return session?.session_token;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LOG_TAG} Game session ticket request failed: {e.Message}");
                EmitValidationIfAny(e);
                OnAuthError?.Invoke(e);
                throw;
            }
        }

        // ── Internals ────────────────────────────────────────────────────────────

        private async Task<AuthSession> ProcessAccessTokenAsync(string accessToken, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(accessToken))
                throw new OperationCanceledException("Empty access token");

            var me = await _api.GetMeAsync(accessToken, ct);
            ApplySession(accessToken, me);
            OnTokenReceived?.Invoke(accessToken);
            return new AuthSession { accessToken = accessToken, me = me };
        }

        private async Task<AuthSession> TryRefreshOrReloginAsync(CancellationToken ct)
        {
            string refreshToken = _storage.LoadRefreshToken();

            if (!string.IsNullOrEmpty(refreshToken))
            {
                try
                {
                    var tokensPair = await _api.RefreshTokenAsync(refreshToken, ct);
                    var me = await _api.GetMeAsync(tokensPair.access_token, ct);

                    ApplySession(tokensPair.access_token, me);
                    _storage.SaveRefreshToken(tokensPair.refresh_token);
                    OnTokenReceived?.Invoke(tokensPair.access_token);

                    return new AuthSession { accessToken = tokensPair.access_token, me = Me };
                }
                catch (Exception e) when (IsHttpStatus(e, 400) || IsHttpStatus(e, 401))
                {
                    Debug.LogWarning($"{LOG_TAG} Refresh token is invalid/expired, will relogin. Reason: {e.Message}");
                    _storage.SaveRefreshToken(null);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"{LOG_TAG} Network/Server error during refresh: {e.Message}");
                    throw;
                }
            }

            return await ReLoginByPolicyAsync(ct);
        }

        private async Task<AuthSession> ReLoginByPolicyAsync(CancellationToken ct)
        {
            // In-memory Me is authoritative; fall back to persisted flag on fresh page load
            bool? isGuest = Me?.is_guest ?? _storage.LoadIsGuest();

            if (isGuest == false)
            {
                Debug.LogWarning($"{LOG_TAG} OpenID session expired. Clearing auth state.");
                ClearStoredTokens();
                throw new InvalidOperationException("OpenID session expired. User must log in again.");
            }

            if (string.IsNullOrEmpty(DeviceId))
                DeviceId = _storage.LoadDeviceId();

            if (string.IsNullOrEmpty(DeviceId))
                throw new InvalidOperationException(
                    "Guest re-login required but DeviceId is unknown. Call LoginGuestAsync(deviceId) at least once.");

            try
            {
                await LoginGuestAsync(DeviceId, ct);
                return new AuthSession { accessToken = AccessToken, me = Me };
            }
            catch (Exception gex) when (IsHttpStatus(gex, 409))
            {
                Debug.LogWarning($"{LOG_TAG} Guest login returned 409 during re-login. Device is linked to OpenID account. Clearing auth state.");
                _storage.SaveIsGuest(false);
                ClearStoredTokens();
                throw;
            }
        }

        private async Task<AuthSession> LoginOpenIdCoreAsync(TimeSpan timeout, CancellationToken ct)
        {
            string codeVerifier = PkceUtils.GenerateCodeVerifier();
            string codeChallenge = PkceUtils.GenerateCodeChallenge(codeVerifier);

            string url = _urlBuilder.BuildAuthPageUrl(isRegistration: false, codeChallenge: codeChallenge);
            var authCode = await _browser.RunFlowAsync(
                url,
                "enjoylix_auth",
                json => _flow.HandlePostMessage(json),
                (t, linkedCt) => _flow.WaitForResultAsync(t, linkedCt),
                timeout,
                ct);
            return await ProcessAuthCodeAsync(authCode, codeVerifier, ct);
        }

        private async Task<AuthSession> TryRestoreSessionAsync(CancellationToken ct)
        {
            await _authGate.WaitAsync(ct);
            try
            {
                if (!string.IsNullOrEmpty(AccessToken))
                {
                    try
                    {
                        var me = await _api.GetMeAsync(AccessToken, ct);
                        ApplySession(AccessToken, me);
                        return new AuthSession { accessToken = AccessToken, me = Me };
                    }
                    catch (Exception e) when (IsHttpStatus(e, 401))
                    {
                        Debug.Log($"{LOG_TAG} Stored access token rejected, trying refresh token.");
                    }
                }

                string refreshToken = _storage.LoadRefreshToken();
                if (string.IsNullOrEmpty(refreshToken))
                    return null;

                try
                {
                    var tokensPair = await _api.RefreshTokenAsync(refreshToken, ct);
                    var me = await _api.GetMeAsync(tokensPair.access_token, ct);

                    ApplySession(tokensPair.access_token, me);
                    _storage.SaveRefreshToken(tokensPair.refresh_token);
                    OnTokenReceived?.Invoke(tokensPair.access_token);

                    return new AuthSession { accessToken = tokensPair.access_token, me = Me };
                }
                catch (Exception e) when (IsHttpStatus(e, 400) || IsHttpStatus(e, 401))
                {
                    Debug.LogWarning($"{LOG_TAG} Refresh token is invalid/expired, browser login required. Reason: {e.Message}");
                    ClearStoredTokens();
                    return null;
                }
            }
            finally
            {
                _authGate.Release();
            }
        }

        private async Task<AuthSession> LoginOnStartNativeCoreAsync(TimeSpan timeout, CancellationToken ct)
        {
            string codeVerifier = PkceUtils.GenerateCodeVerifier();
            string codeChallenge = PkceUtils.GenerateCodeChallenge(codeVerifier);

            string url = _urlBuilder.BuildAuthPageUrl(
                isRegistration: false,
                codeChallenge: codeChallenge,
                deviceId: DeviceId,
                instaReg: true);

            var authCode = await _browser.RunFlowAsync(
                url,
                "enjoylix_auth",
                json => _flow.HandlePostMessage(json),
                (t, linkedCt) => _flow.WaitForResultAsync(t, linkedCt),
                timeout,
                ct);
            return await ProcessAuthCodeAsync(authCode, codeVerifier, ct);
        }

        public async Task<AuthSession> LoginOnStartWebCore(TimeSpan timeout, CancellationToken ct)
        {
            Debug.Log($"{LOG_TAG} LoginOnStartWebCore called");
            string codeVerifier = PkceUtils.GenerateCodeVerifier();
            string codeChallenge = PkceUtils.GenerateCodeChallenge(codeVerifier);

            var authCode = await _browser.RunFlowOnStartAsync(
                codeChallenge,
                json => _flow.HandlePostMessage(json),
                (t, linkedCt) => _flow.WaitForResultAsync(t, linkedCt),
                timeout,
                ct);
            return await ProcessAuthCodeAsync(authCode, codeVerifier, ct);
        }

        private async Task<AuthSession> ProcessAuthCodeAsync(string authCode, string codeVerifier, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(authCode))
                throw new OperationCanceledException("Empty auth code (login canceled or failed)");
            if (string.IsNullOrEmpty(codeVerifier))
                throw new OperationCanceledException("Empty code verifier (login canceled or failed)");

            var tokensPair = await _api.GetTokensPairAsync(authCode, codeVerifier, ct);
            _storage.SaveRefreshToken(tokensPair.refresh_token);
            return await ProcessAccessTokenAsync(tokensPair.access_token, ct);
        }

        private void ApplySession(string token, OpenIdUserResponse me)
        {
            AccessToken = token;
            Me = me;
            _sessionToken = token;
            _sessionExpiresUtc = JwtUtils.TryGetExpiryUtc(token, out var expiry) ? expiry : (DateTime?)null;
            _storage.SaveIsGuest(me.is_guest);
            Debug.Log($"{LOG_TAG} Session updated. Me: id={Me?.id} guest={Me?.is_guest} token_len={AccessToken?.Length ?? 0}");
            if (!string.IsNullOrEmpty(token))
                _storage.SaveAccessToken(token);

            OnSessionApplied?.Invoke();
        }

        private bool HasUnexpiredSession()
        {
            return Me != null
                && !string.IsNullOrEmpty(AccessToken)
                && AccessToken == _sessionToken
                && _sessionExpiresUtc.HasValue
                && DateTime.UtcNow < _sessionExpiresUtc.Value - ExpirySkew;
        }

        private void InvalidateSession()
        {
            _sessionToken = null;
            _sessionExpiresUtc = null;
        }

        private void EmitValidationIfAny(Exception e)
        {
            if (e is EnjoylixApiException apiEx && apiEx.ValidationError?.detail?.Count > 0)
                OnValidationError?.Invoke(apiEx.ValidationError);
        }

        private void ClearStoredTokens()
        {
            AccessToken = null;
            InvalidateSession();
            _storage.SaveAccessToken(null);
            _storage.SaveRefreshToken(null);
        }

        private static bool IsHttpStatus(Exception ex, int statusCode)
        {
            return ex is EnjoylixApiException apiEx && (int)apiEx.StatusCode == statusCode;
        }
    }
}
