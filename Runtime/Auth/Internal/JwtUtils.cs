using System;
using System.Text;
using UnityEngine;

namespace EnjoylixSDK.Auth.Internal
{
    public static class JwtUtils
    {
        [Serializable]
        private class ExpClaim
        {
            public long exp;
        }

        public static bool TryGetExpiryUtc(string jwt, out DateTime expiryUtc)
        {
            expiryUtc = default;

            if (string.IsNullOrEmpty(jwt))
                return false;

            var parts = jwt.Split('.');
            if (parts.Length != 3)
                return false;

            try
            {
                var payload = Base64UrlDecode(parts[1]);
                if (payload == null)
                    return false;

                var claim = JsonUtility.FromJson<ExpClaim>(Encoding.UTF8.GetString(payload));
                if (claim == null || claim.exp <= 0)
                    return false;

                expiryUtc = DateTimeOffset.FromUnixTimeSeconds(claim.exp).UtcDateTime;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static byte[] Base64UrlDecode(string input)
        {
            var s = input.Replace('-', '+').Replace('_', '/');

            switch (s.Length % 4)
            {
                case 0: break;
                case 2: s += "=="; break;
                case 3: s += "="; break;
                default: return null;
            }

            return Convert.FromBase64String(s);
        }
    }
}
