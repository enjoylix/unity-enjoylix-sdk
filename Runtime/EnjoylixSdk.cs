using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EnjoylixSDK.AgeVerification;
using EnjoylixSDK.Attribution;
using EnjoylixSDK.Auth;
using EnjoylixSDK.Auth.Internal;
using EnjoylixSDK.Auth.Models;
using EnjoylixSDK.Common;
using EnjoylixSDK.Config;
using EnjoylixSDK.Net;
using EnjoylixSDK.Purchase;
using EnjoylixSDK.Purchase.Models;
using UnityEngine;

namespace EnjoylixSDK
{
    public class EnjoylixSdk
    {
        // Public Api

        public event Action<string> OnPopupBlocked;

        // Auth Api
        public event Action<LoginKind, bool> OnLoginCompleted;
        public bool IsEmailLogin => _auth.IsEmailLogin;
        public bool IsKnownFullAccount => _auth.IsKnownNonGuest;
        public string EnjoylixId => _auth.Me?.id;
        public string AccessToken => _auth.AccessToken;
        public async Task<string> CreateGameSessionTicketAsync(CancellationToken ct = default) => await _auth.CreateGameSessionTicketAsync(ct);
        public async Task<AuthSession> LoginOpenIdAsync() => await _auth.LoginOpenIdAsync(TimeSpan.FromMinutes(5));
        public async Task<AuthSession> EnsureValidTokenAsync(CancellationToken ct = default) => await _auth.EnsureValidTokenAsync(ct);
        public async Task<AuthSession> LinkGuestAsync(string deviceId) => await _auth.LinkGuestAsync(deviceId, TimeSpan.FromMinutes(5));
        public async Task<AuthSession> PerformLoginWithTokenAsync(string token, CancellationToken ct = default) => await _auth.TokenLoginAsync(token, ct);
        public Task<string> LoginGuestAsync(string deviceId) => _auth.LoginGuestAsync(deviceId);
        public async Task<AuthSession> LoginOnStartWeb(CancellationToken ct = default) => await _auth.LoginOnStartWeb(TimeSpan.FromMinutes(2), ct);
        public void Initialize() => _auth.Initialize();

        public async Task<AuthSession> LoginOnStartAsync(string deviceId, CancellationToken ct = default)
        {
            if (IsWebPortal)
                return await _auth.LoginOnStartWeb(TimeSpan.FromMinutes(2), ct);

            return await _auth.LoginOnStartAsync(deviceId, TimeSpan.FromMinutes(5), ct);
        }

        public void Logout()
        {
            _auth.Logout();
            _attribution.ClearAuthId();
            _attribution.StopPing();
        }

        // Purchase Api

        public async Task<PaymentResult> PurchaseCreateAsync(
            PurchaseCreateRequest req,
            TimeSpan timeout,
            CancellationToken ct = default)
        {
            return await WithAuthRetryAsync(
                token => _purchase.CreateOpenAndWaitAsync(token, req, timeout, ct),
                ct);
        }

        // AgeVerification Api

        public async Task<bool> IsAgeVerificationNeededAsync(CancellationToken ct = default)
        {
            return await WithAuthRetryAsync(
                token => _ageVerification.IsVerificationNeededAsync(token, ct),
                ct);
        }

        public async Task<bool> StartAgeVerificationAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            await _auth.EnsureValidTokenAsync(ct);
            return await _ageVerification.StartVerificationAsync(_auth.AccessToken, timeout, ct);
        }

        // Attribution Api

        public void SetPartner(string partner)
            => _attribution.UpdateContext(new AttributionContext { Partner = partner });

        public void SetPlayerId(string playerId)
            => _attribution.UpdateContext(new AttributionContext { PlayerId = playerId });

        public void TrackLogin() => _attribution.TrackLogin();
        public void TrackRegistration() => _attribution.TrackRegistration();
        public void TrackTutorialComplete() => _attribution.TrackTutorialComplete();
        public void TrackPurchase(string eventType, string source, string checkoutId, string transactionId,
            float amount, string currencyCode, bool isQaPurchase)
            => _attribution.TrackPurchase(eventType, source, checkoutId, transactionId, amount, currencyCode, isQaPurchase);

        /// <summary>
        /// True when this WebGL build runs inside the Enjoylix portal, as decided by <see cref="EnjoylixConfig.WebPortalMode"/>.
        /// Always false in the Editor and on native platforms.
        /// </summary>
        public bool IsWebPortal { get; }

        // Services
        private AuthService _auth;
        private PurchaseService _purchase;
        private AgeVerificationService _ageVerification;
        private AttributionService _attribution;

        public EnjoylixSdk(EnjoylixConfig config, string platform, Dictionary<string, string> additionalAttributionContext = null)
        {
            if (config == null)
            {
                Debug.LogError("EnjoylixSdk initialization failed: config is null");
                throw new ArgumentNullException(nameof(config));
            }

            if (string.IsNullOrEmpty(platform))
            {
                Debug.LogError("EnjoylixSdk initialization failed: platform is null or empty");
                throw new ArgumentException("Platform must be provided", nameof(platform));
            }

            IsWebPortal = EnvironmentDetector.IsWebPortal(config.WebPortalMode);

            var sessionQueryParams = new SessionQueryParams(Application.absoluteURL);
            var urlBuilder = new UrlBuilder(config, platform, additionalAttributionContext, sessionQueryParams);
            var webBrowserService = new WebBrowserService();
            InitializeAttribution(config, urlBuilder, platform, sessionQueryParams);
            InitializeAuthentication(config, urlBuilder, webBrowserService);
            InitializePurchase(config, urlBuilder, webBrowserService);
            InitializeAgeVerification(urlBuilder, webBrowserService);
        }

        private void InitializeAuthentication(EnjoylixConfig config, UrlBuilder urlBuilder, WebBrowserService webBrowserService)
        {
            _auth = new AuthService(urlBuilder, webBrowserService, config);
            _auth.OnPopupBlocked += HandlePopupBlocked;
            _auth.OnLoginCompleted += HandleLoginCompleted;
            _auth.OnSessionApplied += HandleSessionApplied;
        }

        private void InitializePurchase(EnjoylixConfig config, UrlBuilder urlBuilder, WebBrowserService webBrowserService)
        {
            var purchaseApi = new PurchaseApiClient(urlBuilder, config.ActiveEnvironment?.RequestSignSecret);
            var purchaseFlow = new PurchaseFlow();
            _purchase = new PurchaseService(purchaseFlow, purchaseApi, config, webBrowserService);
            _purchase.OnPopupBlocked += HandlePopupBlocked;
        }

        private void InitializeAgeVerification(UrlBuilder urlBuilder, WebBrowserService webBrowserService)
        {
            var ageVerificationApi = new AgeVerificationApiClient(urlBuilder);
            _ageVerification = new AgeVerificationService(ageVerificationApi, urlBuilder, webBrowserService);
            _ageVerification.OnPopupBlocked += HandlePopupBlocked;
        }

        private void InitializeAttribution(EnjoylixConfig config, UrlBuilder urlBuilder, string platform, SessionQueryParams sessionQueryParams)
        {
            var ctx = new AttributionContext
            {
                RawDeeplinkUrl = Application.absoluteURL,
                PlatformId = platform,
                Partner = config.Partner
            };

            _attribution = new AttributionService(urlBuilder, ctx, platform, ResolveEnjoylixAuthId, sessionQueryParams);
        }

        private string ResolveEnjoylixAuthId()
        {
            return !string.IsNullOrEmpty(_auth?.Me?.id) ? _auth.Me.id : _auth?.GuestId;
        }

        public void UpdateAttributionContext(AttributionContext ctx)
            => _attribution.UpdateContext(ctx);

        private async Task<T> WithAuthRetryAsync<T>(Func<string, Task<T>> operation, CancellationToken ct)
        {
            await _auth.EnsureValidTokenAsync(ct);

            try
            {
                return await operation(_auth.AccessToken);
            }
            catch (EnjoylixApiException e) when ((int)e.StatusCode == 401)
            {
                Debug.Log($"Access token was rejected, refreshing session and retrying once. {e.Message}");
                await _auth.ForceRefreshAsync(ct);
                return await operation(_auth.AccessToken);
            }
        }

        private void HandleSessionApplied()
        {
            _attribution.StartSessionPing();
        }

        private void HandlePopupBlocked(string url)
        {
            Debug.LogWarning($"Popup was blocked when trying to open url: {url}");
            OnPopupBlocked?.Invoke(url);
        }

        private void HandleLoginCompleted(LoginKind loginKind, bool isComplete)
        {
            OnLoginCompleted?.Invoke(loginKind, isComplete);
        }
    }
}
