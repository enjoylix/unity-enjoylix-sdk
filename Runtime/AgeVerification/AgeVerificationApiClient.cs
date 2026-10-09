using System.Threading;
using System.Threading.Tasks;
using EnjoylixSDK.AgeVerification.Models;
using EnjoylixSDK.Net;
using UnityEngine;
using UnityEngine.Networking;

namespace EnjoylixSDK.AgeVerification
{
    public sealed class AgeVerificationApiClient
    {
        private readonly UrlBuilder _urlBuilder;

        public AgeVerificationApiClient(UrlBuilder urlBuilder)
        {
            _urlBuilder = urlBuilder;
        }

        public async Task<CheckVerificationNeededResponse> CheckVerificationNeededAsync(string accessToken, CancellationToken ct = default)
        {
            string url = _urlBuilder.BuildCheckVerificationNeededUrl();

            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            await req.SendWebRequestAsTask(ct);

            if (req.result != UnityWebRequest.Result.Success)
                throw ApiClientHelper.CreateException($"{nameof(AgeVerificationApiClient)}.CheckVerificationNeeded", req);

            return JsonUtility.FromJson<CheckVerificationNeededResponse>(req.downloadHandler.text);
        }
    }
}
