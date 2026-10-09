using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace EnjoylixSDK.Auth.Internal
{
	public class DeepLinkQueryParser
	{
		private const string _logTag = "[DeepLinkQueryParser]";

		public static Dictionary<string, string> Parse(string deeplinkUrl)
		{
			Dictionary<string, string> queryParams = new Dictionary<string, string>();

			if (string.IsNullOrEmpty(deeplinkUrl))
			{
				Debug.LogWarning($"{_logTag} Deep link URL is null or empty");
				return queryParams;
			}

			if (!Uri.TryCreate(deeplinkUrl, UriKind.Absolute, out Uri uri))
			{
				Debug.LogError($"{_logTag} Invalid URL: {deeplinkUrl}");
				return queryParams;
			}

			string query = uri.Query;
			if (query.StartsWith("?"))
				query = query.Substring(1);

			// Regex pattern for parsing: key=value, value can be empty
			string pattern = @"(?<key>[^=&]+)=(?<value>[^&]*)";
			foreach (Match m in Regex.Matches(query, pattern))
			{
				string key = Uri.UnescapeDataString(m.Groups["key"].Value);
				string value = Uri.UnescapeDataString(m.Groups["value"].Value);
				queryParams[key] = value;
			}

			return queryParams;
		}
	}
}