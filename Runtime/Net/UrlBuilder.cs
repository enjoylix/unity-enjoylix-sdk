using System.Collections.Generic;
using System.Text;
using EnjoylixSDK.Common;
using EnjoylixSDK.Config;
using UnityEngine;
using UnityEngine.Networking;

namespace EnjoylixSDK.Net
{
    public class UrlBuilder
    {
        private readonly EnjoylixConfig _settings;
        private readonly string _platform;
        private readonly Dictionary<string, string> _additionalAttributionContext;
        private readonly SessionQueryParams _sessionQueryParams;

        public UrlBuilder(
            EnjoylixConfig settings,
            string platform,
            Dictionary<string, string> additionalAttributionContext = null,
            SessionQueryParams sessionQueryParams = null)
        {
            _additionalAttributionContext = additionalAttributionContext;
            _sessionQueryParams = sessionQueryParams;
            _settings = settings;
            _platform = platform;
        }

        public string BuildGuestLoginApiUrl(string deviceId)
        {
            var baseUrl = _settings.ActiveEnvironment.BaseServiceUrl;

            var paramsMap = new Dictionary<string, string>
            {
                { "device_id", deviceId },
                { "platform", _platform },
            };

            if(_additionalAttributionContext != null)
            {
                foreach (var kvp in _additionalAttributionContext)
                {
                    if (!paramsMap.ContainsKey(kvp.Key))
                    {
                        paramsMap.Add(kvp.Key, kvp.Value);
                    }
                }
            }
            var endpoint = Endpoints.Auth.GuestLogin.Replace("{project_name}", _settings.ProjectName);
            return Construct(baseUrl, endpoint, paramsMap);
        }

        public string BuildAuthPageUrl(
            bool isRegistration,
            string codeChallenge = null,
            string token = null,
            string deviceId = null,
            bool instaReg = false)
        {
            var baseUrl = _settings.ActiveEnvironment.BaseServiceUrl;
            var path = isRegistration ? Endpoints.Auth.LinkGuestPage : Endpoints.Auth.LoginPage;

            var paramsMap = new Dictionary<string, string>
            {
                    { "project_name", _settings.ProjectName },
                    { "lang", GetSystemLanguageShortCode() },
                    { "platform", _platform }
            };

            if (!string.IsNullOrEmpty(token))
            {
                paramsMap.Add("token", token);
            }

            if (!string.IsNullOrEmpty(deviceId))
            {
                paramsMap.Add("device_id", deviceId);
            }

            if (instaReg)
            {
                paramsMap.Add("insta_reg", "true");
            }

            if (Application.isMobilePlatform)
            {
                paramsMap.Add("return_type", "deeplink");
                paramsMap.Add("redirect_url", _settings.DeepLinkScheme);
            }
            else if (Application.isEditor)
            {
                paramsMap.Add("return_type", "deeplink");
                paramsMap.Add("redirect_url", _settings.DeepLinkScheme);
            }
            else
            {
                paramsMap.Add("return_type", "post_message");
            }

            if (!string.IsNullOrEmpty(codeChallenge))
            {
                paramsMap.Add("code_challenge", codeChallenge);
            }

            if(_additionalAttributionContext != null)
            {
                foreach (var kvp in _additionalAttributionContext)
                {
                    if (!paramsMap.ContainsKey(kvp.Key))
                    {
                        paramsMap.Add(kvp.Key, kvp.Value);
                    }
                }
            }

            return Construct(baseUrl, path, paramsMap);
        }

        public string BuildUserInfoApiUrl()
        {
            var baseUrl = _settings.ActiveEnvironment.BaseServiceUrl;
            return Construct(baseUrl, Endpoints.Auth.UserInfo, null);
        }

        public string BuildGetTokensPairUrl()
        {
            var baseUrl = _settings.ActiveEnvironment.BaseServiceUrl;
            var endpoint = Endpoints.Auth.GetTokensPair.Replace("{project_name}", _settings.ProjectName);
            return Construct(baseUrl, endpoint, _sessionQueryParams?.Values);
        }

        public string BuildRefreshTokenUrl()
        {
            var baseUrl = _settings.ActiveEnvironment.BaseServiceUrl;
            return Construct(baseUrl, Endpoints.Auth.RefreshToken, null);
        }

        public string BuildGameSessionUrl()
        {
            var baseUrl = _settings.ActiveEnvironment.BaseServiceUrl;
            return Construct(baseUrl, Endpoints.Auth.GameSession, null);
        }

        public string BuildCheckVerificationNeededUrl()
        {
            var baseUrl = _settings.ActiveEnvironment.BaseServiceUrl;
            var endpoint = Endpoints.Auth.CheckVerificationNeeded.Replace("{project_name}", _settings.ProjectName);
            var paramsMap = new Dictionary<string, string>
            {
                    { "platform", _platform }
            };
            return Construct(baseUrl, endpoint, paramsMap);
        }

        public string BuildRequestVerificationUrl(string accessToken)
        {
            var baseUrl = _settings.ActiveEnvironment.BaseServiceUrl;
            var path = Endpoints.Auth.RequestVerification;
            
            var paramsMap = new Dictionary<string, string>
            {
                    { "project_name", _settings.ProjectName },
            };

            if (!string.IsNullOrEmpty(accessToken))
            {
                paramsMap.Add("access_token", accessToken);
            }

            if (Application.isMobilePlatform)
            {
                paramsMap.Add("return_type", "deeplink");
                paramsMap.Add("redirect_url", _settings.DeepLinkScheme);
            }
            else if (Application.isEditor)
            {
                paramsMap.Add("return_type", "deeplink");
                paramsMap.Add("redirect_url", _settings.DeepLinkScheme);
            }
            else
            {
                paramsMap.Add("return_type", "post_message");
            }
            return Construct(baseUrl, path, paramsMap);
        }

        public string BuildAttributionUrl()
        {
            var endpoint = Endpoints.Attribution.SendEvent.Replace("{project_name}", _settings.ProjectName);
            return _settings.ActiveEnvironment.AttributionServiceUrl + endpoint;
        }

        private string Construct(string host, string path, IReadOnlyDictionary<string, string> queryParams)
        {
            var sb = new StringBuilder();
            
            sb.Append(host.TrimEnd('/'));
            
            if (!path.StartsWith("/")) sb.Append('/');
            sb.Append(path);

            if (queryParams != null && queryParams.Count > 0)
            {
                sb.Append('?');
                bool first = true;
                foreach (var kvp in queryParams)
                {
                    if (!first) sb.Append('&');
                    sb.Append(UnityWebRequest.EscapeURL(kvp.Key));
                    sb.Append('=');
                    sb.Append(UnityWebRequest.EscapeURL(kvp.Value));
                    first = false;
                }
            }

            return sb.ToString();
        }

        public string BuildPurchaseCreateUrl()
        {
            var baseUrl = _settings.ActiveEnvironment.BaseServiceUrl;
            return $"{baseUrl}/api/v1/purchase/create";
        }

        public string BuildPurchaseCompleteUrl()
        {
            var baseUrl = _settings.ActiveEnvironment.BaseServiceUrl;
            return $"{baseUrl}/api/v1/purchase/complete";
        }

        public string BuildPurchaseCheckoutUrl()
        {
            var baseUrl = _settings.ActiveEnvironment.BaseServiceUrl;
            return $"{baseUrl}/api/v1/purchase/checkout";
        }

        public string BuildPurchaseStatusUrl(string paymentId)
        {
            var baseUrl = _settings.ActiveEnvironment.BaseServiceUrl;
            return $"{baseUrl}/api/v1/purchase/status?payment_id={UnityWebRequest.EscapeURL(paymentId)}";
        }

        public string BuildCoinSpendStatusUrl(string gameName, string gameTransactionId)
        {
            var baseUrl = _settings.ActiveEnvironment.BaseServiceUrl;
            return $"{baseUrl}/api/v1/purchase/coin_spend_status" +
                   $"?game_name={UnityWebRequest.EscapeURL(gameName)}" +
                   $"&game_transaction_id={UnityWebRequest.EscapeURL(gameTransactionId)}";
        }

        private string GetSystemLanguageShortCode()
        {
            return Application.systemLanguage switch
            {
                SystemLanguage.Russian => "ru",
                SystemLanguage.German => "de",
                SystemLanguage.Chinese => "cn",
                _ => "en"
            };
        }
    }
}