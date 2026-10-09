using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace EnjoylixSDK.Auth.Internal
{
    public sealed class AuthFlow
    {
        private const string LOG_TAG = "[AuthFlow]";
        private TaskCompletionSource<string> _tcs;

        public Task<string> WaitForResultAsync(TimeSpan timeout, CancellationToken ct)
        {
            if (_tcs != null && !_tcs.Task.IsCompleted)
            {
                Debug.Log($"{LOG_TAG} Login already in progress. Canceling previous.");
                _tcs.TrySetCanceled();
            }

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
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
            if (!AuthDeepLinkParser.TryParse(deeplinkUrl, out var authCode))
                return false;

            Publish(authCode);
            return true;
        }

        public bool HandlePostMessage(string json)
        {
            if (!AuthPostMessageParser.TryParse(json, out var authCode))
                return false;

            Publish(authCode);
            return true;
        }

        private void Publish(string token)
        {
            _tcs?.TrySetResult(token);
        }
    }
}
