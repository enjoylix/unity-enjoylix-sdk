using System;
using System.Threading;
using System.Threading.Tasks;
using EnjoylixSDK.Common;
using EnjoylixSDK.Net;
using UnityEngine;
using Exception = System.Exception;

namespace EnjoylixSDK.Auth.Internal
{
    public class WebBrowserService
    {
        // Grace period: Android delivers deepLinkActivated slightly after OnApplicationFocus.
        // 700ms covers the observed 100-300ms gap on tested devices with headroom.
        private const int DismissGraceMs = 700;

        private readonly WebGlPopupHandler _webGlPopupHandler;
        private bool _browserWasOpened;
        private bool _lostFocusAfterOpen;
        private Action _onDismissed;
        private CancellationTokenSource _pendingDismissCts;

        public WebBrowserService()
        {
            _webGlPopupHandler = new WebGlPopupHandler();
            ApplicationFocusListener.Instance.FocusChanged += OnFocusChanged;
        }

        // Call before Application.OpenURL — registers dismiss callback for when user returns without result.
        public void OpenExternalBrowser(string url, Action onDismissed = null)
        {
            Application.OpenURL(url);
            _browserWasOpened = true;
            _lostFocusAfterOpen = false;
            _onDismissed = onDismissed;
        }

        // Call when result arrived — cancels dismiss tracking.
        public void Reset()
        {
            _pendingDismissCts?.Cancel();
            _pendingDismissCts = null;
            _browserWasOpened = false;
            _lostFocusAfterOpen = false;
            _onDismissed = null;
        }

        private void OnFocusChanged(bool hasFocus)
        {
            if (_browserWasOpened && !hasFocus)
            {
                _lostFocusAfterOpen = true;
                return;
            }

            if (_browserWasOpened && hasFocus && _lostFocusAfterOpen)
                ScheduleDismiss();
        }

        private void ScheduleDismiss()
        {
            _pendingDismissCts?.Cancel();
            _pendingDismissCts = new CancellationTokenSource();

            var token = _pendingDismissCts.Token;
            var callback = _onDismissed;

            Task.Delay(DismissGraceMs).ContinueWith(_ =>
            {
                if (token.IsCancellationRequested) return;
                if (!_browserWasOpened) return;
                Reset();
                callback?.Invoke();
            });
        }

        public async Task<T> RunFlowOnStartAsync<T>(
            string codeChallenge,
            Func<string, bool> onPostMessage,
            Func<TimeSpan, CancellationToken, Task<T>> waitResult,
            TimeSpan timeout,
            CancellationToken ct)
        {
            Debug.Log($"[WebBrowserService] RunFlowOnStartAsync called");
            var timeoutCts = SdkDelayRunner.Instance.CreateTimeoutSource(timeout, ct);
            try
            {
                var resultTask = waitResult(timeout, timeoutCts.Token);
                var json = await SendPostMessageAndWaitMessageAsync(codeChallenge, timeoutCts.Token);
                if (!onPostMessage(json))
                    throw new Exception($"Failed to parse result from popup: {json}");
                return await resultTask;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Debug.LogError($"[WebBrowserService] Portal login timed out after {timeout}");
                throw new FlowTimeoutException("portal_login", timeout);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebBrowserService] Error while postMessage: {e.Message}");
                throw;
            }
            finally
            {
                timeoutCts.Cancel();
                timeoutCts.Dispose();
            }
        }

        public async Task<T> RunFlowAsync<T>(
            string url,
            string webGlWindowName,
            Func<string, bool> onPostMessage,
            Func<TimeSpan, CancellationToken, Task<T>> waitResult,
            TimeSpan timeout,
            CancellationToken ct)
        {
            try
            {
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    var timeoutCts = SdkDelayRunner.Instance.CreateTimeoutSource(timeout, ct);
                    try
                    {
                        var resultTask = waitResult(timeout, timeoutCts.Token);
                        var json = await OpenPopupAndWaitMessageAsync(url, webGlWindowName, timeoutCts.Token);
                        if (!onPostMessage(json))
                            throw new Exception($"Failed to parse result from popup: {json}");
                        return await resultTask;
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        Debug.LogError($"[WebBrowserService] Popup flow '{webGlWindowName}' timed out after {timeout}");
                        throw new FlowTimeoutException(webGlWindowName, timeout);
                    }
                    finally
                    {
                        timeoutCts.Cancel();
                        timeoutCts.Dispose();
                    }
                }

                using var dismissCts = new CancellationTokenSource();
                OpenExternalBrowser(url, onDismissed: () => dismissCts.Cancel());
                try
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, dismissCts.Token);
                    return await waitResult(timeout, linked.Token);
                }
                catch (OperationCanceledException) when (dismissCts.IsCancellationRequested)
                {
                    throw new UserDismissedException();
                }
                finally
                {
                    Reset();
                }
            }
            finally
            {
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                    ClosePopup();
            }
        }

        // ── WebGL proxy ────────────────────────────────────────────────────────

        public Task<string> SendPostMessageAndWaitMessageAsync(string codeChallenge, CancellationToken ct)
            => _webGlPopupHandler.SendPostMessageAndWaitMessageAsync(codeChallenge, ct);

        public Task<string> OpenPopupAndWaitMessageAsync(string url, string windowName, CancellationToken ct)
            => _webGlPopupHandler.OpenAndWaitMessageAsync(url, windowName, ct);

        public void ClosePopup()
            => _webGlPopupHandler.Close();
    }
}
