using System;
using System.Threading;
using System.Threading.Tasks;
using EnjoylixSDK.Net;
using UnityEngine;

namespace EnjoylixSDK.Attribution.Internal
{
	internal static class AttributionPingScheduler
	{
		private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

		private static CancellationTokenSource _cts;
		private static bool _isQuitHookRegistered;

		public static void Schedule(TimeSpan interval, Action onTick)
		{
			if (interval <= TimeSpan.Zero || onTick == null) return;

			Stop();
			RegisterQuitHook();

			_cts = new CancellationTokenSource();
			_ = RunAsync(interval, onTick, _cts.Token);
		}

		public static void Stop()
		{
			if (_cts == null) return;

			_cts.Cancel();
			_cts.Dispose();
			_cts = null;
		}

		private static async Task RunAsync(TimeSpan interval, Action onTick, CancellationToken ct)
		{
			var nextTickUtc = DateTime.UtcNow + interval;

			while (true)
			{
				try
				{
					await SdkDelayRunner.Instance.DelayAsync((int)PollInterval.TotalMilliseconds, ct);
				}
				catch (OperationCanceledException)
				{
					return;
				}

				if (ct.IsCancellationRequested) return;
				if (DateTime.UtcNow < nextTickUtc) continue;

				nextTickUtc = DateTime.UtcNow + interval;

				try
				{
					onTick();
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}
		}

		private static void RegisterQuitHook()
		{
			if (_isQuitHookRegistered) return;

			Application.quitting += Stop;
			_isQuitHookRegistered = true;
		}
	}
}
