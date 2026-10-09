using System.Collections.Generic;
using System.Text;

namespace EnjoylixSDK.Attribution.Internal
{
	internal static class SimpleJson
	{
		public static string Escape(string s)
		{
			if (s == null) return "";
			return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
		}

		public static string DictToJson(IReadOnlyDictionary<string, string> dict)
		{
			if (dict == null) return "{}";

			var sb = new StringBuilder();
			sb.Append('{');
			bool first = true;

			foreach (var kv in dict)
			{
				if (!first) sb.Append(',');
				sb.Append('"').Append(Escape(kv.Key)).Append('"').Append(':');
				sb.Append('"').Append(Escape(kv.Value)).Append('"');
				first = false;
			}

			sb.Append('}');
			return sb.ToString();
		}
	}
}