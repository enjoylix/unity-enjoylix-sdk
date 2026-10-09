using System;
using EnjoylixSDK.Auth.Internal;

namespace EnjoylixSDK.AgeVerification.Internal
{
	public static class AgeVerificationDeepLinkParser
	{
		public static bool TryParse(string deeplinkUrl, out bool success)
		{
			success = false;

			var q = DeepLinkQueryParser.Parse(deeplinkUrl);
			if (q == null || q.Count == 0)
				return false;

			if (!q.TryGetValue("type", out var type))
				return false;

			if (!string.Equals(type, "age_verification_result", StringComparison.OrdinalIgnoreCase))
				return false;

			if (!q.TryGetValue("success", out var s))
				return false;

			success = s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
			return true;
		}
	}
}