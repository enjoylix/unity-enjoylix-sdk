namespace EnjoylixSDK.Auth.Internal
{
    public static class AuthDeepLinkParser
    {
        public static bool TryParse(string deeplinkUrl, out string token)
        {
            token = null;
            var query = DeepLinkQueryParser.Parse(deeplinkUrl);
            if (query == null) return false;
            if (!query.TryGetValue("type", out var t) || !query.TryGetValue("auth_code", out var ac))
                return false;
            if (t != "auth_result" || string.IsNullOrEmpty(ac))
                return false;
            token = ac;
            return true;
        }
    }
}