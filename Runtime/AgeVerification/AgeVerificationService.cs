using System;
using System.Threading;
using System.Threading.Tasks;
using EnjoylixSDK.AgeVerification.Internal;
using EnjoylixSDK.Auth.Internal;
using EnjoylixSDK.Net;
using UnityEngine;

namespace EnjoylixSDK.AgeVerification
{
    public sealed class AgeVerificationService
    {
        private const string LOG_TAG = "[AgeVerification]";

        private readonly AgeVerificationApiClient _api;
        private readonly UrlBuilder _urlBuilder;
        private readonly AgeVerificationFlow _flow;
        private readonly WebBrowserService _browser;

        public event Action<string> OnPopupBlocked;

        public AgeVerificationService(AgeVerificationApiClient api, UrlBuilder urlBuilder, WebBrowserService browser)
        {
            _api = api;
            _urlBuilder = urlBuilder;
            _flow = new AgeVerificationFlow();
            _browser = browser;
            DeepLinkListener.Instance.RegisterHandler(_flow.HandleDeepLink);
        }

        public async Task<bool> IsVerificationNeededAsync(string accessToken, CancellationToken ct = default)
        {
            var check = await _api.CheckVerificationNeededAsync(accessToken, ct);
            return check.age_verification_needed;
        }

        public async Task<bool> StartVerificationAsync(
            string accessToken,
            TimeSpan timeout,
            CancellationToken ct = default)
        {
            var url = _urlBuilder.BuildRequestVerificationUrl(accessToken);

            try
            {
                var result = await _browser.RunFlowAsync(
                    url,
                    "enjoylix_age_verification",
                    json => _flow.HandlePostMessage(json),
                    (t, linkedCt) => _flow.WaitForResultAsync(t, linkedCt),
                    timeout,
                    ct);

                Debug.Log($"{LOG_TAG} Verification result: {result.success}");
                return result.success;
            }
            catch (PopupBlockedException e)
            {
                OnPopupBlocked?.Invoke(e.Url);
                Debug.LogWarning($"{LOG_TAG} Popup was blocked for url {e.Url}");
                return false;
            }
        }

    }
}
