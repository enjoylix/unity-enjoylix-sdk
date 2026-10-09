using UnityEngine;

namespace EnjoylixSDK.Attribution.Internal
{
	internal static class AttributionContract
	{
		public const string DefaultCurrencyCode = "USD";

		private const string LOG_TAG = "[EnjoylixAttributionService]";

		public static string NormalizeAuthId(string authId)
		{
			if (string.IsNullOrEmpty(authId)) return "";

			if (LooksLikeJwt(authId))
			{
				Debug.LogError($"{LOG_TAG} auth_id looks like an access token, not an Enjoylix user id "
						+ "(expected the 'id' field of /api/v1/users/me). Sending the event without auth_id.");
				return "";
			}

			return authId;
		}

		public static string NormalizeCurrencyCode(string currencyCode)
		{
			var normalized = currencyCode == null ? "" : currencyCode.Trim().ToUpperInvariant();

			if (IsIso4217(normalized)) return normalized;

			Debug.LogError($"{LOG_TAG} currency_code '{currencyCode}' is not an ISO 4217 code "
					+ $"(expected 3 letters, e.g. 'USD'). Falling back to '{DefaultCurrencyCode}'.");
			return DefaultCurrencyCode;
		}

		private static bool LooksLikeJwt(string value)
		{
			return value.StartsWith("ey", System.StringComparison.Ordinal)
					&& value.Split('.').Length == 3;
		}

		private static bool IsIso4217(string value)
		{
			if (value.Length != 3) return false;

			foreach (var c in value)
			{
				if (c < 'A' || c > 'Z') return false;
			}

			return true;
		}
	}
}
