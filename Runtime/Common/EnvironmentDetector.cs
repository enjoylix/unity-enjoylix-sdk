using System;
using System.Runtime.InteropServices;
using EnjoylixSDK.Config;
using UnityEngine;

namespace EnjoylixSDK.Common
{
	/// <summary>
	/// Decides whether a WebGL build is running inside the Enjoylix portal.
	/// The Editor and native platforms are never "portal".
	/// </summary>
	internal static class EnvironmentDetector
	{
		private const string LOG_TAG = "[EnjoylixSDK][EnvironmentDetector]";

		private static PortalMode? _cachedMode;
		private static bool _cachedResult;

#if UNITY_WEBGL && !UNITY_EDITOR
		[DllImport("__Internal")]
		private static extern int Enjoylix_IsEmbeddedInIframe();

		[DllImport("__Internal")]
		private static extern IntPtr Enjoylix_GetAncestorOrigins();

		[DllImport("__Internal")]
		private static extern IntPtr Enjoylix_GetDocumentReferrer();

		[DllImport("__Internal")]
		private static extern void Enjoylix_FreeMemory(IntPtr ptr);

		private static string ReadAndFree(IntPtr ptr)
		{
			if (ptr == IntPtr.Zero)
				return string.Empty;

			try
			{
				return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
			}
			finally
			{
				Enjoylix_FreeMemory(ptr);
			}
		}
#endif

		/// <summary>
		/// <see cref="PortalMode.Auto"/>: portal when the page is embedded in an iframe (<c>window.self !== window.top</c>).
		/// <c>document.referrer</c> is no longer consulted — browsers strip it in private modes and under
		/// restrictive Referrer-Policy, which made the old detection fail silently.
		/// The result is computed once per mode and cached for the lifetime of the player.
		/// </summary>
		public static bool IsWebPortal(PortalMode mode)
		{
			if (_cachedMode == mode)
				return _cachedResult;

			_cachedResult = Detect(mode);
			_cachedMode = mode;
			return _cachedResult;
		}

		private static bool Detect(PortalMode mode)
		{
#if UNITY_WEBGL && !UNITY_EDITOR
			switch (mode)
			{
				case PortalMode.Portal:
					Debug.Log($"{LOG_TAG} WebPortalMode=Portal — treating the build as running inside the Enjoylix portal.");
					return true;

				case PortalMode.Standalone:
					Debug.Log($"{LOG_TAG} WebPortalMode=Standalone — treating the build as a standalone web page.");
					return false;
			}

			try
			{
				bool embedded = Enjoylix_IsEmbeddedInIframe() != 0;

				// Diagnostics only — neither value is reliable enough to decide on.
				string ancestorOrigins = ReadAndFree(Enjoylix_GetAncestorOrigins());
				string referrer = ReadAndFree(Enjoylix_GetDocumentReferrer());
				Debug.Log($"{LOG_TAG} WebPortalMode=Auto: embedded={embedded}, ancestorOrigins=\"{ancestorOrigins}\", referrer=\"{referrer}\" → isWebPortal={embedded}");

				return embedded;
			}
			catch (Exception e)
			{
				Debug.LogWarning($"{LOG_TAG} Failed to detect the portal environment, assuming standalone: {e.Message}");
				return false;
			}
#else
			if (mode == PortalMode.Portal)
				Debug.LogWarning($"{LOG_TAG} WebPortalMode=Portal is ignored outside WebGL player builds.");

			return false;
#endif
		}
	}
}
