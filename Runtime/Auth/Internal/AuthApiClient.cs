using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnjoylixSDK.Auth.Models;
using EnjoylixSDK.Net;
using UnityEngine;
using UnityEngine.Networking;

namespace EnjoylixSDK.Auth.Internal
{
    public sealed class AuthApiClient
    {
        private readonly UrlBuilder _urlBuilder;

        public AuthApiClient(UrlBuilder urlBuilder)
        {
            _urlBuilder = urlBuilder;
        }

        public async Task<GuestLoginResponse> GuestLoginAsync(string deviceId, CancellationToken ct = default)
        {
            string url = _urlBuilder.BuildGuestLoginApiUrl(deviceId);

            using var req = await HttpRetry.SendAsync(
                () => UnityWebRequest.Get(url),
                $"{nameof(AuthApiClient)}.GuestLogin",
                ct);

            return JsonUtility.FromJson<GuestLoginResponse>(req.downloadHandler.text);
        }

        public async Task<OpenIdUserResponse> GetMeAsync(string accessToken, CancellationToken ct = default)
        {
            string url = _urlBuilder.BuildUserInfoApiUrl();

            using var req = await HttpRetry.SendAsync(
                () => CreateAuthorizedGetRequest(url, accessToken),
                $"{nameof(AuthApiClient)}.GetMe",
                ct);

            return JsonUtility.FromJson<OpenIdUserResponse>(req.downloadHandler.text);
        }

        public async Task<TokensPair> GetTokensPairAsync(string authCode, string codeVerify, CancellationToken ct = default)
        {
            string url = _urlBuilder.BuildGetTokensPairUrl();

            var payload = new GetTokensPayload { auth_code = authCode, code_verify = codeVerify };
            string json = JsonUtility.ToJson(payload);

            using var req = await HttpRetry.SendAsync(
                () => CreateJsonPostRequest(url, json),
                $"{nameof(AuthApiClient)}.GetTokensPair",
                ct);

            return JsonUtility.FromJson<TokensPair>(req.downloadHandler.text);
        }

        public async Task<TokensPair> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        {
            string url = _urlBuilder.BuildRefreshTokenUrl();

            var payload = new RefreshTokenPayload { refresh_token = refreshToken };
            string json = JsonUtility.ToJson(payload);

            using var req = CreateJsonPostRequest(url, json);
            await req.SendWebRequestAsTask(ct);

            if (req.result != UnityWebRequest.Result.Success)
                throw ApiClientHelper.CreateException($"{nameof(AuthApiClient)}.RefreshToken", req);

            return JsonUtility.FromJson<TokensPair>(req.downloadHandler.text);
        }

        public async Task<GameSessionResponse> CreateGameSessionAsync(string accessToken, string gameName, CancellationToken ct = default)
        {
            string url = _urlBuilder.BuildGameSessionUrl();

            var payload = new GameSessionPayload { game_name = gameName };
            string json = JsonUtility.ToJson(payload);

            using var req = CreateJsonPostRequest(url, json);
            req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            await req.SendWebRequestAsTask(ct);

            if (req.result != UnityWebRequest.Result.Success)
                throw ApiClientHelper.CreateException($"{nameof(AuthApiClient)}.CreateGameSession", req);

            return JsonUtility.FromJson<GameSessionResponse>(req.downloadHandler.text);
        }

        private UnityWebRequest CreateAuthorizedGetRequest(string url, string accessToken)
        {
            var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            return req;
        }

        private UnityWebRequest CreateJsonPostRequest(string url, string json)
        {
            var req = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            return req;
        }
    }
}
