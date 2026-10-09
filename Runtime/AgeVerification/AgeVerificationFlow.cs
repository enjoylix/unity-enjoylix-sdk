using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace EnjoylixSDK.AgeVerification.Internal
{
    public sealed class AgeVerificationFlow
    {
        private const string LOG_TAG = "[AgeVerificationFlow]";
        private TaskCompletionSource<AgeVerificationResult> _tcs;

        public Task<AgeVerificationResult> WaitForResultAsync(TimeSpan timeout, CancellationToken ct)
        {
            if (_tcs != null && !_tcs.Task.IsCompleted)
            {
                Debug.Log($"{LOG_TAG} Verification already in progress. Canceling previous.");
                _tcs.TrySetCanceled();
            }

            var tcs = new TaskCompletionSource<AgeVerificationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _tcs = tcs;

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(timeout);
            linkedCts.Token.Register(() => tcs.TrySetCanceled(linkedCts.Token));
            tcs.Task.ContinueWith(_ => linkedCts.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);

            return tcs.Task;
        }

        public bool HandleDeepLink(string deeplinkUrl)
        {
            if (!AgeVerificationDeepLinkParser.TryParse(deeplinkUrl, out var success))
                return false;

            Publish(new AgeVerificationResult { success = success, raw = deeplinkUrl });
            return true;
        }

        public bool HandlePostMessage(string json)
        {
            if (!AgeVerificationPostMessageParser.TryParse(json, out var success))
                return false;

            Publish(new AgeVerificationResult { success = success, raw = json });
            return true;
        }

        private void Publish(AgeVerificationResult result)
        {
            _tcs?.TrySetResult(result);
        }
    }
}
