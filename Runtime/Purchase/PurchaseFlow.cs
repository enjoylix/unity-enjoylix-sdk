using System;
using System.Threading;
using System.Threading.Tasks;
using EnjoylixSDK.Auth.Internal;
using EnjoylixSDK.Purchase.Internal;
using EnjoylixSDK.Purchase.Models;
using UnityEngine;

namespace EnjoylixSDK.Purchase
{
    public sealed class PurchaseFlow
    {
        private const string LOG_TAG = "[PurchaseFlow]";
        private TaskCompletionSource<PaymentResult> _tcs;
        private string _activeTransactionId;

        public Task<PaymentResult> WaitForResultAsync(string pid, TimeSpan timeout, CancellationToken ct = default)
        {
            if (_tcs != null && !_tcs.Task.IsCompleted)
            {
                Debug.Log($"{LOG_TAG} Purchase already in progress. Canceling previous.");
                _tcs.TrySetCanceled();
                _activeTransactionId = null;
            }

            _activeTransactionId = pid;
            var tcs = new TaskCompletionSource<PaymentResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _tcs = tcs;

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(timeout);
            linkedCts.Token.Register(() =>
            {
                if (tcs.TrySetCanceled(linkedCts.Token))
                    _activeTransactionId = null;
            });
            tcs.Task.ContinueWith(_ => linkedCts.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);

            return tcs.Task;
        }

        public bool HandleDeepLink(string deeplinkUrl)
        {
            var q = DeepLinkQueryParser.Parse(deeplinkUrl);
            if (q == null || q.Count == 0) return false;

            if (!q.TryGetValue("success", out var s)) return false;
            if (!q.TryGetValue("payment_id", out var pid) || string.IsNullOrEmpty(pid)) return false;
            if (!q.TryGetValue("game_transaction_id", out var transactionId) || string.IsNullOrEmpty(transactionId)) return false;

            bool success = s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);

            PublishIfMatches(new PaymentResult
            {
                success = success,
                payment_id = pid,
                raw = deeplinkUrl,
                transaction_id = transactionId,
            });

            return true;
        }

        public bool HandlePostMessage(string json)
        {
            if (PaymentPostMessageParser.TryParse(json, out var success, out var pid, out var transactionId))
            {
                PublishIfMatches(new PaymentResult { success = success, payment_id = pid, raw = json, transaction_id = transactionId });
                return true;
            }
            return false;
        }

        private void PublishIfMatches(PaymentResult result)
        {
            if (string.IsNullOrEmpty(_activeTransactionId)) return;
            if (string.IsNullOrEmpty(result.transaction_id)) return;
            if (!string.Equals(result.transaction_id, _activeTransactionId, StringComparison.Ordinal)) return;
            Publish(result);
        }

        private void Publish(PaymentResult res)
        {
            if (_tcs == null || _tcs.Task.IsCompleted) return;
            if (_tcs.TrySetResult(res))
                _activeTransactionId = null;
        }
    }
}
