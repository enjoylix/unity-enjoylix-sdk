using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnjoylixSDK.Net;
using EnjoylixSDK.Purchase.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace EnjoylixSDK.Purchase
{
    public sealed class PurchaseApiClient
    {
        private readonly UrlBuilder _urlBuilder;
        private readonly RequestSigner _signer;

        public PurchaseApiClient(UrlBuilder urlBuilder, string requestSignSecret = null)
        {
            _urlBuilder = urlBuilder;
            _signer = new RequestSigner(requestSignSecret);
        }

        public async Task<PurchaseCreateResponse> CreateAsync(string authAccessToken, PurchaseCreateRequest body, CancellationToken ct = default)
        {
            var url = _urlBuilder.BuildPurchaseCreateUrl();
            using var req = MakeSignedJsonPost(url, body);
            req.SetRequestHeader("Authorization", $"Bearer {authAccessToken}");
            await req.SendWebRequestAsTask(ct);

            if (req.result != UnityWebRequest.Result.Success)
                throw ApiClientHelper.CreateException($"{nameof(PurchaseApiClient)}.Create", req);

            return JsonUtility.FromJson<PurchaseCreateResponse>(req.downloadHandler.text);
        }

        public async Task<string> CompleteAsync(string authAccessToken, PurchaseCompleteRequest body, CancellationToken ct = default)
        {
            var url = _urlBuilder.BuildPurchaseCompleteUrl();
            using var req = MakeJsonPost(url, body);
            req.SetRequestHeader("Authorization", $"Bearer {authAccessToken}");
            await req.SendWebRequestAsTask(ct);

            if (req.result != UnityWebRequest.Result.Success)
                throw ApiClientHelper.CreateException($"{nameof(PurchaseApiClient)}.Complete", req);

            return req.downloadHandler.text;
        }

        public async Task<PurchaseStatusResponse> StatusAsync(string authAccessToken, string paymentId, CancellationToken ct = default)
        {
            var url = _urlBuilder.BuildPurchaseStatusUrl(paymentId);
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Authorization", $"Bearer {authAccessToken}");
            await req.SendWebRequestAsTask(ct);

            if (req.result != UnityWebRequest.Result.Success)
                throw ApiClientHelper.CreateException($"{nameof(PurchaseApiClient)}.Status", req);

            return JsonUtility.FromJson<PurchaseStatusResponse>(req.downloadHandler.text);
        }

        public async Task<CoinSpendStatusResponse> CoinSpendStatusAsync(
            string authAccessToken, string gameName, string gameTransactionId, CancellationToken ct = default)
        {
            var url = _urlBuilder.BuildCoinSpendStatusUrl(gameName, gameTransactionId);
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Authorization", $"Bearer {authAccessToken}");
            await req.SendWebRequestAsTask(ct);

            if (req.result != UnityWebRequest.Result.Success)
                throw ApiClientHelper.CreateException($"{nameof(PurchaseApiClient)}.CoinSpendStatus", req);

            return JsonUtility.FromJson<CoinSpendStatusResponse>(req.downloadHandler.text);
        }

        private static UnityWebRequest MakeJsonPost(string url, object body)
        {
            var json = JsonUtility.ToJson(body);
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            return req;
        }

        private UnityWebRequest MakeSignedJsonPost(string url, PurchaseCreateRequest body)
        {
            var payload = Encoding.UTF8.GetBytes(ToSignableJson(body));
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(payload);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            var signature = _signer.Sign(payload);
            if (signature != null)
                req.SetRequestHeader(RequestSigner.Header, signature);

            return req;
        }

        private static string ToSignableJson(PurchaseCreateRequest body)
        {
            var sb = new StringBuilder();
            sb.Append("{\"coin_amount\":").Append(body.coin_amount.ToString(CultureInfo.InvariantCulture));
            AppendStringField(sb, "game_name", body.game_name);
            AppendStringField(sb, "transaction_id", body.transaction_id);
            AppendStringField(sb, "transaction_image_url", body.transaction_image_url);
            AppendStringField(sb, "transaction_name", body.transaction_name);
            return sb.Append('}').ToString();
        }

        private static void AppendStringField(StringBuilder sb, string name, string value)
        {
            sb.Append(",\"").Append(name).Append("\":\"");

            foreach (var c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ' || c > '~') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
        }
    }
}
