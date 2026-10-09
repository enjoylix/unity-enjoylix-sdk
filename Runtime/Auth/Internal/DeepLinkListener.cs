using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace EnjoylixSDK.Auth.Internal
{
	public class DeepLinkListener : MonoBehaviour
	{
		private static DeepLinkListener _instance;
		private TaskCompletionSource<string> _tcs;
		private readonly List<Func<string, bool>> _handlers = new();

		public static DeepLinkListener Instance
		{
			get
			{
				if (_instance != null) return _instance;

				var go = new GameObject("[Enjoylix] DeepLinkListener");
				DontDestroyOnLoad(go);
				_instance = go.AddComponent<DeepLinkListener>();
				return _instance;
			}
		}

		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				Destroy(gameObject);
				return;
			}

			_instance = this;

			Application.deepLinkActivated += OnDeepLink;

			if (!string.IsNullOrEmpty(Application.absoluteURL))
				OnDeepLink(Application.absoluteURL);
		}

		public void RegisterHandler(Func<string, bool> handler)
		{
			if (handler == null) return;
			if (_handlers.Contains(handler)) return;
			_handlers.Add(handler);
		}

		public Task<string> WaitForNextUrlAsync(TimeSpan? timeout = null)
		{
			_tcs = new TaskCompletionSource<string>();

			if (timeout.HasValue)
				_ = TimeoutAsync(_tcs, timeout.Value);

			return _tcs.Task;
		}

		private async Task TimeoutAsync(TaskCompletionSource<string> tcs, TimeSpan timeout)
		{
			await Task.Delay(timeout);
			tcs.TrySetException(new TimeoutException("Deep link timeout"));
		}

		private void OnDeepLink(string url)
		{
			for (int i = 0; i < _handlers.Count; i++)
			{
				try
				{
					if (_handlers[i]?.Invoke(url) == true)
						break;
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}
		}

#if UNITY_EDITOR
		public void SimulateDeepLink(string url)
		{
			OnDeepLink(url);
		}
#endif
	}
}