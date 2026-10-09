using System.Collections.Generic;
using EnjoylixSDK.Auth.Internal;

namespace EnjoylixSDK.Common
{
	public class SessionQueryParams
	{
		private Dictionary<string, string> _values = new Dictionary<string, string>();
		private string _sourceUrl;

		public SessionQueryParams(string sourceUrl = null)
		{
			SetSource(sourceUrl);
		}

		public IReadOnlyDictionary<string, string> Values => _values;

		public void SetSource(string sourceUrl)
		{
			if (string.IsNullOrEmpty(sourceUrl) || sourceUrl == _sourceUrl) return;

			_sourceUrl = sourceUrl;
			_values = DeepLinkQueryParser.Parse(sourceUrl);
		}
	}
}
