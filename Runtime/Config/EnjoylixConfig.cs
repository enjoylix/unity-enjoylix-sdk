using UnityEngine;

namespace EnjoylixSDK.Config
{
    [CreateAssetMenu(fileName = "EnjoylixConfig", menuName = "Enjoylix/EnjoylixConfig")]
    public class EnjoylixConfig : ScriptableObject
    {
        [Header("General")]
        public string ProjectName = "test_game";
        public string DeepLinkScheme = "test_scheme://com.test_company.test_app/test_path";

        [Tooltip("Partner identifier agreed with Enjoylix at the integration stage. Sent with every attribution event.")]
        public string Partner = "test_partner";

        [Header("Environments")]
        public EnvironmentEnum CurrentEnvironment = EnvironmentEnum.Dev;

        [Space]
        public EnjoylixEnvironment DevEnvironment;
        public EnjoylixEnvironment ProdEnvironment;

        public EnjoylixEnvironment ActiveEnvironment => CurrentEnvironment == EnvironmentEnum.Prod ? ProdEnvironment : DevEnvironment;

        [Header("Security")] [Tooltip("Unique salt for local data encryption. Change this to a random string.")]
        public string StorageEncryptionSalt = "r@nd0m_str1ng_$@lt";

        [Header("Web")]
        [Tooltip("WebGL only. Auto: portal when the page is embedded in an iframe. Portal / Standalone: force the mode regardless of how the page is embedded. " +
                 "Set Standalone if you embed the WebGL build in an iframe on your own site.")]
        public PortalMode WebPortalMode = PortalMode.Auto;
    }
}
