using EnjoylixSDK.Common;
using EnjoylixSDK.Config;

namespace EnjoylixSDK.Auth.Internal
{
	internal static class AuthStorageFactory
	{
		public static IAuthStorage Create(EnjoylixConfig config)
		{
			if (EnvironmentDetector.IsWebPortal(config.WebPortalMode))
				return new InMemoryAuthStorage();

			return new PlayerPrefsAuthStorage(config.StorageEncryptionSalt);
		}
	}
}