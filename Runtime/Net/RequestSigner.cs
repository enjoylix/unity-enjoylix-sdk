using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace EnjoylixSDK.Net
{
	internal sealed class RequestSigner
	{
		public const string Header = "X-Signature";

		private const string LogTag = "[Enjoylix::RequestSigner]";

		private readonly byte[] _secret;

		public RequestSigner(string secret)
		{
			if (string.IsNullOrEmpty(secret))
			{
				Debug.LogWarning($"{LogTag} RequestSignSecret is empty on the active environment — signed requests will go out without a signature.");
				return;
			}

			_secret = Encoding.UTF8.GetBytes(secret);
		}

		public string Sign(byte[] body)
		{
			if (_secret == null) return null;

			using var hmac = new HMACSHA256(_secret);
			return ToHex(hmac.ComputeHash(body));
		}

		private static string ToHex(byte[] hash)
		{
			var sb = new StringBuilder(hash.Length * 2);
			foreach (var b in hash) sb.Append(b.ToString("x2"));
			return sb.ToString();
		}
	}
}
