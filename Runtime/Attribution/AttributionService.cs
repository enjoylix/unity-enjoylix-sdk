using System;
using System.Text;
using System.Threading.Tasks;
using EnjoylixSDK.Attribution.Internal;
using EnjoylixSDK.Attribution.Models;
using EnjoylixSDK.Common;
using EnjoylixSDK.Config;
using EnjoylixSDK.Net;
using UnityEngine;
using UnityEngine.Networking;

namespace EnjoylixSDK.Attribution
{
    public class AttributionService : IAttributionService
    {
        private static readonly TimeSpan PingInterval = TimeSpan.FromHours(6);

        private readonly EnjoylixConfig _config;
        private readonly UrlBuilder _urlBuilder;
        private readonly SessionQueryParams _sessionQueryParams;
        private readonly string _platform;
        private readonly Func<string> _authIdProvider;
        private AttributionContext _context;
        private string _pingAccountId;
        private bool _isPingRunning;

        public AttributionService(UrlBuilder urlBuilder, AttributionContext context, string platform, Func<string> authIdProvider, SessionQueryParams sessionQueryParams = null)
        {
            _urlBuilder = urlBuilder;
            _context = context;
            _platform = string.IsNullOrWhiteSpace(platform) ? Application.platform.ToString() : platform;
            _authIdProvider = authIdProvider;
            _sessionQueryParams = sessionQueryParams ?? new SessionQueryParams(context.RawDeeplinkUrl);

            Debug.Log("[EnjoylixAttributionService] Initialized with query params: "
                    + SimpleJson.DictToJson(_sessionQueryParams.Values));
        }

        public void TrackRegistration()
        {
            var eventData = CreateLoginRegistrationData();
            var payload = CreateBasePayload("registration", eventData);
            _ = SendEventAsync(payload);
        }

        public void TrackLogin()
        {
            var eventData = CreateLoginRegistrationData();
            var payload = CreateBasePayload("login", eventData);
            _ = SendEventAsync(payload);

            StartSessionPing();
        }

        public void StartSessionPing()
        {
            var accountId = ResolveAuthId();

            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(_context.PlayerId)) return;

            if (_isPingRunning && _pingAccountId == accountId) return;

            _pingAccountId = accountId;
            _isPingRunning = true;

            TrackPing();
            AttributionPingScheduler.Schedule(PingInterval, TrackPing);
        }

        public void TrackPing()
        {
            var payload = CreateBasePayload("ping", null);
            _ = SendEventAsync(payload);
        }

        public void StopPing()
        {
            _isPingRunning = false;
            _pingAccountId = null;
            AttributionPingScheduler.Stop();
        }

        public void TrackPurchase(string eventType, string source, string checkoutId, string transactionId, float amount, string currencyCode, bool isQaPurchase)
        {

            var eventData = new PurchaseEventData
            {
                source = source,
                checkout_id = checkoutId,
                transaction_id = transactionId,
                amount = amount,
                currency_code = AttributionContract.NormalizeCurrencyCode(currencyCode),
                is_qa_purchase = isQaPurchase
            };

            var payload = CreateBasePayload(eventType, eventData);
            _ = SendEventAsync(payload);
        }

        public void TrackTutorialComplete()
        {

            var eventData = new TutorialEventData { is_completed = true };
            var payload = CreateBasePayload("tutorial:complete", eventData);
            _ = SendEventAsync(payload);
        }

        private AttributionEventBase CreateBasePayload(string eventName, object eventData)
        {
            return new AttributionEventBase
            {
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    player_id = _context.PlayerId ?? "",
                    auth_id = ResolveAuthIdForEvent(eventName),
                    partner = _context.Partner ?? "",
                    platform = _platform,
                    @event = eventName,
                    event_data_json = eventData != null ? JsonUtility.ToJson(eventData) : "{}"
            };
        }

        private LoginRegistrationEventData CreateLoginRegistrationData()
        {
            return new LoginRegistrationEventData
            {
                    player_country = _context.CountryCode ?? "XX",
                    platform_id = _context.PlatformId ?? _platform,
                    client_ip = _context.ClientIp ?? "127.0.0.1",
                    query_params_json = SimpleJson.DictToJson(_sessionQueryParams.Values),
                    user_agent = "",
                    forwarded_for = "",
                    referer = ""
            };
        }
        
        public void UpdateContext(AttributionContext ctx)
        {
            if (ctx == null) return;

            if (!string.IsNullOrEmpty(ctx.Partner)) _context.Partner = ctx.Partner;
            if (!string.IsNullOrEmpty(ctx.PlayerId)) _context.PlayerId = ctx.PlayerId;
            if (!string.IsNullOrEmpty(ctx.AuthId)) _context.AuthId = AttributionContract.NormalizeAuthId(ctx.AuthId);
            if (!string.IsNullOrEmpty(ctx.CountryCode)) _context.CountryCode = ctx.CountryCode;
            if (!string.IsNullOrEmpty(ctx.ClientIp)) _context.ClientIp = ctx.ClientIp;
            if (!string.IsNullOrEmpty(ctx.PlatformId)) _context.PlatformId = ctx.PlatformId;

            if (!string.IsNullOrEmpty(ctx.RawDeeplinkUrl) && ctx.RawDeeplinkUrl != _context.RawDeeplinkUrl)
            {
                _context.RawDeeplinkUrl = ctx.RawDeeplinkUrl;
                _sessionQueryParams.SetSource(ctx.RawDeeplinkUrl);
            }

            StartSessionPing();
        }

        public void ClearAuthId()
        {
            _context.AuthId = null;
        }

        private string ResolveAuthIdForEvent(string eventName)
        {
            var authId = ResolveAuthId();

            if (string.IsNullOrEmpty(authId))
                Debug.LogWarning($"[EnjoylixAttributionService] auth_id is empty, event '{eventName}' "
                        + "will not be attributed to an Enjoylix account.");

            return authId;
        }

        private string ResolveAuthId()
        {
            var authId = AttributionContract.NormalizeAuthId(_authIdProvider?.Invoke());

            if (string.IsNullOrEmpty(authId))
                authId = AttributionContract.NormalizeAuthId(_context.AuthId);

            return authId;
        }

        private async Task SendEventAsync(AttributionEventBase payload)
        {
            string jsonBody = BuildFinalJson(payload);
            string url = _urlBuilder.BuildAttributionUrl();

            using var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            try
            {
                await request.SendWebRequestAsTask(default);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[AttributionService] Error sending event '{payload.@event}': {request.error}\nResponse: {request.downloadHandler.text}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AttributionService] Exception sending event '{payload.@event}': {e.Message}");
            }
        }

        private static string BuildFinalJson(AttributionEventBase p)
        {
            static string Esc(string s) => SimpleJson.Escape(s);
            string eventData = string.IsNullOrWhiteSpace(p.event_data_json) ? "{}" : p.event_data_json;

            return "{"
                   + $"\"timestamp\":{p.timestamp},"
                   + $"\"player_id\":\"{Esc(p.player_id)}\","
                   + $"\"auth_id\":\"{Esc(p.auth_id)}\","
                   + $"\"partner\":\"{Esc(p.partner)}\","
                   + $"\"platform\":\"{Esc(p.platform)}\","
                   + $"\"event\":\"{Esc(p.@event)}\","
                   + $"\"event_data\":{eventData}"
                   + "}";
        }
    }
}
