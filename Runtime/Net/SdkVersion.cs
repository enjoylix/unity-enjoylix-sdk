using UnityEngine;

namespace EnjoylixSDK.Net
{
	internal static class SdkVersion
	{
		public const string PackageName = "com.enjoylix.sdk";

		/// <summary>Name of the Resources text asset baked at build time (without extension).</summary>
		public const string ResourceName = "EnjoylixSdkVersion";

		/// <summary>Fallback used when a real version cannot be resolved (e.g. local dev checkout).</summary>
		public const string Unknown = "dev";

		private static string _cached;

		public static string Current => _cached ??= Resolve();

		private static string Resolve()
		{
#if UNITY_EDITOR
			var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(SdkVersion).Assembly);
			if (info != null && IsRealVersion(info.version))
				return info.version;
#endif
			var asset = Resources.Load<TextAsset>(ResourceName);
			if (asset != null && IsRealVersion(asset.text))
				return asset.text.Trim();

			return Unknown;
		}

		private static bool IsRealVersion(string version)
			=> !string.IsNullOrWhiteSpace(version) && version.Contains(".");
	}
}
