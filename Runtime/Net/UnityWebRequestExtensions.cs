using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace EnjoylixSDK.Net
{
	internal static class UnityWebRequestExtensions
	{
		private const string CallerIdentityHeader = "X-Caller-Identity";
		private const string CallerSource = "unity-sdk";

		private static string CallerIdentity => $"{CallerSource}/{SdkVersion.Current}";

		public static Task<UnityWebRequest> SendWebRequestAsTask(this UnityWebRequest request, CancellationToken ct)
		{
			request.SetRequestHeader(CallerIdentityHeader, CallerIdentity);

			var tcs = new TaskCompletionSource<UnityWebRequest>();
			var op = request.SendWebRequest();

			// cancel
			if (ct.CanBeCanceled)
			{
				ct.Register(() =>
				{
					try { request.Abort(); } catch { /* ignore */ }
					tcs.TrySetCanceled(ct);
				});
			}

			op.completed += _ =>
			{
				tcs.TrySetResult(request);
			};

			return tcs.Task;
		}
	}
}