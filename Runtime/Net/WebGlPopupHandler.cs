using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AOT;
using EnjoylixSDK.Net;
using UnityEngine;

namespace EnjoylixSDK.Common
{
	public sealed class WebGlPopupHandler
	{
		private const string LOG_TAG = "[WebGlPopupHandler]";
		private const int ClosePollIntervalMs = 500;
		private static TaskCompletionSource<string> _messageTaskSource;

#if (UNITY_WEBGL || UNITY_WEBPLAYER) && !UNITY_EDITOR
		[DllImport("__Internal")]
		private static extern void Enjoylix_SendPortalLoginRequest(string codeChallenge, string targetOrigin, IntPtr callback);

		[DllImport("__Internal")]
		private static extern int Enjoylix_OpenWindow(string url, string windowName, string windowSize, IntPtr callback);

		[DllImport("__Internal")]
		private static extern int Enjoylix_IsWindowClosed(int windowId);

		[DllImport("__Internal")]
		private static extern void Enjoylix_CloseWindow(int windowId);

		private static Action<IntPtr> _cachedCallback;
		private static int _windowId = -1;

		[MonoPInvokeCallback(typeof(Action<IntPtr>))]
		private static void OnMessageReceived(IntPtr jsonStringPtr)
		{
			string jsonString = Marshal.PtrToStringUTF8(jsonStringPtr);
			Debug.Log($"{LOG_TAG} OnMessageReceived raw json: {jsonString}");
			_messageTaskSource?.TrySetResult(jsonString);
		}
#endif

		public async Task<string> SendPostMessageAndWaitMessageAsync(string codeChallenge, CancellationToken ct = default)
		{
#if (UNITY_WEBGL || UNITY_WEBPLAYER) && !UNITY_EDITOR
			if (string.IsNullOrEmpty(codeChallenge))
				throw new ArgumentException("codeChallenge is null or empty", nameof(codeChallenge));

			if (_messageTaskSource != null && !_messageTaskSource.Task.IsCompleted)
			{
				Debug.LogWarning($"{LOG_TAG} Another popup operation is already in progress. Canceling previous one.");
				_messageTaskSource.TrySetCanceled();
			}

			_messageTaskSource = new TaskCompletionSource<string>();

			CancellationTokenRegistration ctr = default;
			if (ct.CanBeCanceled)
			{
				ctr = ct.Register(() =>
				{
					Debug.Log($"{LOG_TAG} Cancellation requested.");
					_messageTaskSource.TrySetCanceled();
				});
			}

			_cachedCallback = OnMessageReceived;
			IntPtr callbackPtr = Marshal.GetFunctionPointerForDelegate(_cachedCallback);

			string targetOrigin = string.Empty;

			try
			{
				Debug.Log($"{LOG_TAG} Sending portal login request. codeChallenge={codeChallenge}, targetOrigin={targetOrigin}");
				Enjoylix_SendPortalLoginRequest(codeChallenge, targetOrigin, callbackPtr);

				var json = await _messageTaskSource.Task;
				Debug.Log($"{LOG_TAG} Portal login response received successfully.");
				return json;
			}
			finally
			{
				ctr.Dispose();
				_messageTaskSource = null;
			}
#else
			Debug.LogError($"{LOG_TAG} Web postMessage is only supported on WebGL platform.");
			throw new NotSupportedException("Web postMessage is only supported on WebGL platform.");
#endif
		}

		public async Task<string> OpenAndWaitMessageAsync(
			string url,
			string windowName,
			CancellationToken ct = default)
		{
#if (UNITY_WEBGL || UNITY_WEBPLAYER) && !UNITY_EDITOR
			if (_messageTaskSource != null && !_messageTaskSource.Task.IsCompleted)
			{
				Debug.LogWarning($"{LOG_TAG} Another popup operation is already in progress. Canceling previous one.");
				_messageTaskSource.TrySetCanceled();
			}

			_messageTaskSource = new TaskCompletionSource<string>();

			CancellationTokenRegistration ctr = default;
			if (ct.CanBeCanceled)
			{
				ctr = ct.Register(() =>
				{
					Debug.Log($"{LOG_TAG} Cancellation requested.");
					_messageTaskSource.TrySetCanceled();
				});
			}

			_cachedCallback = OnMessageReceived;
			IntPtr callbackPtr = Marshal.GetFunctionPointerForDelegate(_cachedCallback);

			Debug.Log($"{LOG_TAG} Opening popup. Url={url}");
			Debug.Log($"{LOG_TAG} Callback ptr={(long)callbackPtr}");

			_windowId = Enjoylix_OpenWindow(url, windowName, "", callbackPtr);

			Debug.Log($"{LOG_TAG} Enjoylix_OpenWindow returned windowId={_windowId}");

			if (_windowId < 0)
			{
				ctr.Dispose();
				_messageTaskSource = null;
				throw new PopupBlockedException(url);
			}

			try
			{
				var messageTask = _messageTaskSource.Task;
				var closeTask = WaitForWindowCloseAsync(_windowId, ct);

				await Task.WhenAny(messageTask, closeTask);

				if (messageTask.Status == TaskStatus.RanToCompletion)
				{
					Debug.Log($"{LOG_TAG} Message received successfully.");
					return messageTask.GetAwaiter().GetResult();
				}

				Debug.LogWarning($"{LOG_TAG} Popup window was closed before message was received.");

				var exception = new UserDismissedException();
				if (!_messageTaskSource.Task.IsCompleted)
					_messageTaskSource.TrySetException(exception);

				return await _messageTaskSource.Task;
			}
			finally
			{
				ctr.Dispose();
				Debug.Log($"{LOG_TAG} Popup flow finished.");
				_messageTaskSource = null;
			}
#else
			Debug.LogError($"{LOG_TAG} Web popup is only supported on WebGL platform.");
			throw new NotSupportedException("Web popup is only supported on WebGL platform.");
#endif
		}

		public bool IsClosed()
		{
#if (UNITY_WEBGL || UNITY_WEBPLAYER) && !UNITY_EDITOR
			if (_windowId < 0)
				return true;

			return Enjoylix_IsWindowClosed(_windowId) == 1;
#else
			return true;
#endif
		}

		public async Task WaitForCloseAsync(CancellationToken ct = default)
		{
#if (UNITY_WEBGL || UNITY_WEBPLAYER) && !UNITY_EDITOR
			if (_windowId < 0)
				return;

			await WaitForWindowCloseAsync(_windowId, ct);
#endif
		}

		public void Close()
		{
#if (UNITY_WEBGL || UNITY_WEBPLAYER) && !UNITY_EDITOR
			if (_windowId < 0)
				return;

			Debug.Log($"{LOG_TAG} Close called. Current windowId={_windowId}");
			Enjoylix_CloseWindow(_windowId);
			_windowId = -1;
#endif
		}

#pragma warning disable CS1998
		private async Task WaitForWindowCloseAsync(int windowId, CancellationToken ct)
#pragma warning restore CS1998
		{
#if (UNITY_WEBGL || UNITY_WEBPLAYER) && !UNITY_EDITOR
			Debug.Log($"{LOG_TAG} Start waiting for window close. windowId={windowId}");

			while (Enjoylix_IsWindowClosed(windowId) != 1)
			{
				ct.ThrowIfCancellationRequested();
				await SdkDelayRunner.Instance.DelayAsync(ClosePollIntervalMs, ct);
			}

			Debug.Log($"{LOG_TAG} Window closed detected. windowId={windowId}");
#endif
		}
	}
}