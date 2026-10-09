using System;
using EnjoylixSDK.Auth.Models;
using UnityEngine;

namespace EnjoylixSDK.Auth.Internal
{
    public static class AuthPostMessageParser
    {
        [Serializable]
        private class PortalLoginResponseDto
        {
            public string type;
            public string auth_code;
            public string error;
        }

        public static bool TryParse(string json, out string token)
        {
            token = null;
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                var portalDto = JsonUtility.FromJson<PortalLoginResponseDto>(json);
                if (portalDto != null &&
                    portalDto.type == "enjoylix_portal_login_result" &&
                    !string.IsNullOrEmpty(portalDto.auth_code))
                {
                    token = portalDto.auth_code;
                    return true;
                }

                var dto = JsonUtility.FromJson<OpenIdFrontendResponse>(json);
                if (dto == null) return false;
                if (dto.type != "auth_result" || string.IsNullOrEmpty(dto.auth_code))
                    return false;
                token = dto.auth_code;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AuthPostMessageParser] Parse exception: {e}");
                return false;
            }
        }
    }
}
