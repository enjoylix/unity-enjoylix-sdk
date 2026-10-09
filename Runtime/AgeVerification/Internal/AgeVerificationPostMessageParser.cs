using System;
using UnityEngine;

namespace EnjoylixSDK.AgeVerification.Internal
{
	[Serializable]
	public class AgeVerificationPostMessageDto
	{
		public string type;
		public bool success;
	}

	public static class AgeVerificationPostMessageParser
	{
		public static bool TryParse(string json, out bool success)
		{
			success = false;

			if (string.IsNullOrWhiteSpace(json))
				return false;

			try
			{
				var dto = JsonUtility.FromJson<AgeVerificationPostMessageDto>(json);
				if (dto == null || dto.type != "age_verification_result")
					return false;

				success = dto.success;
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}