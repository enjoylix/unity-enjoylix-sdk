using UnityEngine;

namespace EnjoylixSDK.Config
{
    [CreateAssetMenu(fileName = "EnjoylixEnvironment", menuName = "Enjoylix/EnjoylixEnvironment", order = 0)]
    public class EnjoylixEnvironment : ScriptableObject
    {
        [Header("Hosts")]
        [Tooltip("Base Host for Authorization and Purchase Services")]
        public string BaseServiceUrl = "https://openid-service-dev-main.dev.eks.playful-fairies.com";

        [Tooltip("Base Host for Attribution Service")]
        public string AttributionServiceUrl = "https://attribution-service-dev-main.dev.eks.playful-fairies.com";

        [Header("Security")]
        [Tooltip("Shared secret the Enjoylix team issues for signing client requests. Replace the placeholder with the real key.")]
        public string RequestSignSecret = "YOUR_SIGN_KEY_PROVIDED_BY_ENJOYLIX_TEAM";
    }
}