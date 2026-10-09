using System;
using System.Security.Cryptography;
using System.Text;

namespace EnjoylixSDK.Auth
{
    public static class PkceUtils
    {
        public static string GenerateCodeVerifier()
        {
            byte[] bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) {
                rng.GetBytes(bytes);
            }

            return Base64UrlEncode(bytes);
        }

        public static string GenerateCodeChallenge(string verifier)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(verifier));
                return Base64UrlEncode(hash);
            }
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }
    }
}