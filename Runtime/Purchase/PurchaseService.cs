using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnjoylixSDK.Auth.Internal;
using EnjoylixSDK.Config;
using EnjoylixSDK.Net;
using EnjoylixSDK.Purchase.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace EnjoylixSDK.Purchase
{
    public sealed class PurchaseService
    {
        private readonly PurchaseApiClient _purchaseApiClient;
        private readonly PurchaseFlow _flow;
        private readonly EnjoylixConfig _settings;
        private readonly WebBrowserService _browser;

        public event Action<string> OnPopupBlocked;

        // Server-side reconciliation of a "dismissed" purchase: on Android the result is
        // delivered via a client-side deep link, which some providers (e.g. Antilopay/SBP)
        // fail to fire on return. The payment still succeeds server-side, so after our normal
        // wait times out into a dismiss, we make a single safety-net check of the coin-spend
        // status before giving up. By this point the provider's server-to-server callback has
        // already landed (the user has finished paying and returned), so one query is enough.
        private const string CompletedStatus = "completed";

        // The check runs while the purchase flow is being torn down, so it must not share the
        // caller's token: the game cancels that token once the browser/popup closes, which
        // aborts the request before it can leave the client. It gets its own budget instead.
        private static readonly TimeSpan ReconcileTimeout = TimeSpan.FromSeconds(10);

        public PurchaseService(
            PurchaseFlow flow,
            PurchaseApiClient purchaseApiClient,
            EnjoylixConfig settings,
            WebBrowserService browser)
        {
            _purchaseApiClient = purchaseApiClient;
            _flow = flow;
            _settings = settings;
            _browser = browser;
            DeepLinkListener.Instance.RegisterHandler(_flow.HandleDeepLink);
        }

        public async Task<PaymentResult> CreateOpenAndWaitAsync(
            string authAccessToken,
            PurchaseCreateRequest req,
            TimeSpan timeout,
            CancellationToken ct = default)
        {
            var create = await CreateAsync(authAccessToken, req, ct);
            var url = GetUrlWithParams(create.transaction_url);

            try
            {
                return await _browser.RunFlowAsync(
                    url,
                    "enjoylix_pay",
                    json => _flow.HandlePostMessage(json),
                    (t, linkedCt) => _flow.WaitForResultAsync(req.transaction_id, t, linkedCt),
                    timeout,
                    ct);
            }
            catch (PopupBlockedException e)
            {
                OnPopupBlocked?.Invoke(e.Url);
                Debug.LogWarning($"[PurchaseService] Popup was blocked for url {e.Url}");
                return null;
            }
            catch (UserDismissedException)
            {
                var reconciled = await TryReconcileAsync(authAccessToken, req);
                if (reconciled != null)
                    return reconciled;
                throw;
            }
        }

        // Single safety-net check: returns a successful PaymentResult only if the server
        // reports the coin spend as completed; otherwise null, so the caller rethrows
        // UserDismissedException. Any non-completed status, 404, or network error means
        // "not completed".
        private async Task<PaymentResult> TryReconcileAsync(
            string authAccessToken, PurchaseCreateRequest req)
        {
            try
            {
                using var cts = new CancellationTokenSource(ReconcileTimeout);
                var status = await _purchaseApiClient.CoinSpendStatusAsync(
                    authAccessToken, req.game_name, req.transaction_id, cts.Token);

                if (status != null &&
                    string.Equals(status.status, CompletedStatus, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[PurchaseService] Dismissed purchase reconciled as completed for transaction {req.transaction_id}");
                    return new PaymentResult
                    {
                        success = true,
                        payment_id = status.id,
                        transaction_id = req.transaction_id,
                    };
                }
            }
            catch (Exception e)
            {
                // 404 (not found / other user), network errors, timeout, etc. — treat as "not completed".
                Debug.LogWarning($"[PurchaseService] Coin spend status safety check failed: {e.GetType().Name}: {e.Message}");
            }

            return null;
        }

        public Task<PurchaseCreateResponse> CreateAsync(
            string authAccessToken,
            PurchaseCreateRequest req,
            CancellationToken ct = default)
        {
            return _purchaseApiClient.CreateAsync(authAccessToken, req, ct);
        }

        public Task<string> CompleteAsync(
            string authAccessToken,
            string paymentId,
            CancellationToken ct = default)
        {
            return _purchaseApiClient.CompleteAsync(
                authAccessToken,
                new PurchaseCompleteRequest { payment_id = paymentId },
                ct);
        }

        public Task<PurchaseStatusResponse> StatusAsync(
            string authAccessToken,
            string paymentId,
            CancellationToken ct = default)
        {
            return _purchaseApiClient.StatusAsync(authAccessToken, paymentId, ct);
        }

        private string GetUrlWithParams(string url)
        {
            var paramsMap = new Dictionary<string, string>
            {
                { "project_name", _settings.ProjectName },
                { "lang", GetSystemLanguageShortCode() }
            };

            if (Application.isMobilePlatform || Application.isEditor)
            {
                paramsMap.Add("return_type", "deeplink");
                paramsMap.Add("redirect_url", _settings.DeepLinkScheme);
            }
            else
            {
                paramsMap.Add("return_type", "post_message");
            }

            return Construct(url, paramsMap);
        }

        private string GetSystemLanguageShortCode()
        {
            return Application.systemLanguage switch
            {
                SystemLanguage.Russian => "ru",
                SystemLanguage.German => "de",
                _ => "en"
            };
        }

        private string Construct(string url, Dictionary<string, string> queryParams)
        {
            var sb = new StringBuilder();
            sb.Append(url);

            if (queryParams != null && queryParams.Count > 0)
            {
                sb.Append('&');
                bool first = true;

                foreach (var kvp in queryParams)
                {
                    if (!first)
                        sb.Append('&');

                    sb.Append(UnityWebRequest.EscapeURL(kvp.Key));
                    sb.Append('=');
                    sb.Append(UnityWebRequest.EscapeURL(kvp.Value));
                    first = false;
                }
            }

            return sb.ToString();
        }
    }
}
