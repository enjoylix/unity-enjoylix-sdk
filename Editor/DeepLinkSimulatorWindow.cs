using EnjoylixSDK.Auth.Internal;
using UnityEditor;
using UnityEngine;

namespace EnjoylixSDK.Editor
{
	public class DeepLinkSimulatorWindow : EditorWindow
	{
		private string _deepLink = "";

		[MenuItem("Enjoylix/Debug/Deep Link Simulator")]
		public static void ShowWindow()
		{
			GetWindow<DeepLinkSimulatorWindow>("DeepLink Sim");
		}

		private void OnGUI()
		{
			GUILayout.Label("Simulate Deep Link (For Editor Testing)", EditorStyles.boldLabel);
        
			GUILayout.Space(10);
			EditorGUILayout.HelpBox("1. Login in the browser.\n2. When redirected to 'enjoylix://...', copy the URL.\n3. Paste it below and click Simulate.", MessageType.Info);

			_deepLink = EditorGUILayout.TextField("URL:", _deepLink);

			if (GUILayout.Button("Simulate Incoming DeepLink"))
			{
				if (Application.isPlaying)
				{
					DeepLinkListener.Instance.SimulateDeepLink(_deepLink);
					Debug.Log("DeepLink simulated!");
				}
				else
				{
					Debug.LogWarning("Enter Play Mode first.");
				}
			}
		}
	}
}