using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace EnjoylixSDK.Net
{
    internal static class HttpRetry
    {
        private const string LOG_TAG = "[HttpRetry]";
        private const int DefaultMaxAttempts = 3;
        private const int BaseDelayMs = 1000;

        public static async Task<UnityWebRequest> SendAsync(
            Func<UnityWebRequest> requestFactory,
            string context,
            CancellationToken ct,
            int maxAttempts = DefaultMaxAttempts)
        {
            for (int attempt = 1; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var req = requestFactory();

                try
                {
                    await req.SendWebRequestAsTask(ct);
                }
                catch
                {
                    req.Dispose();
                    throw;
                }

                if (req.result == UnityWebRequest.Result.Success)
                    return req;

                bool canRetry = attempt < maxAttempts && ApiClientHelper.IsTransient(req);
                var exception = ApiClientHelper.CreateException(context, req, attempt);
                req.Dispose();

                if (!canRetry)
                    throw exception;

                int delayMs = BaseDelayMs * (1 << (attempt - 1));
                Debug.LogWarning(
                    $"{LOG_TAG} {context} attempt {attempt}/{maxAttempts} failed: {exception.Message} Retrying in {delayMs}ms.");

                await SdkDelayRunner.Instance.DelayAsync(delayMs, ct);
            }
        }
    }
}
