using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace EnjoylixSDK.Editor
{
	internal sealed class SdkVersionBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
	{
		private const string PackageName = "com.enjoylix.sdk";

		// Must match EnjoylixSDK.Net.SdkVersion.ResourceName (it is internal to the runtime assembly).
		private const string ResourceName = "EnjoylixSdkVersion";
		private const string Fallback = "dev";

		private const string GeneratedRoot = "Assets/EnjoylixSDK.Generated";
		private const string ResourcesDir = GeneratedRoot + "/Resources";
		private const string AssetPath = ResourcesDir + "/" + ResourceName + ".txt";

		public int callbackOrder => 0;

		public void OnPreprocessBuild(BuildReport report)
		{
			CleanUp();

			var version = ResolveVersion();
			Directory.CreateDirectory(ResourcesDir);
			File.WriteAllText(AssetPath, version);
			AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
		}

		public void OnPostprocessBuild(BuildReport report) => CleanUp();

		private static string ResolveVersion()
		{
			var info = PackageInfo.FindForAssembly(typeof(SdkVersionBuildProcessor).Assembly);
			var version = info?.version;
			if (string.IsNullOrWhiteSpace(version))
			{
				Debug.LogWarning(
					$"[EnjoylixSDK] Could not resolve the package version for '{PackageName}'. " +
					$"Baking '{Fallback}' into the build; outgoing requests will report the SDK as dev.");
				return Fallback;
			}

			return version;
		}

		private static void CleanUp()
		{
			if (AssetDatabase.DeleteAsset(GeneratedRoot))
				return;

			// Fall back to plain file IO if the folder exists but is not tracked by the AssetDatabase.
			if (!Directory.Exists(GeneratedRoot))
				return;

			Directory.Delete(GeneratedRoot, true);
			File.Delete(GeneratedRoot + ".meta");
			AssetDatabase.Refresh();
		}
	}
}
