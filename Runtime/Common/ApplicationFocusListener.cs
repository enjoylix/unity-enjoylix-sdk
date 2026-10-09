using System;
using UnityEngine;

namespace EnjoylixSDK.Common
{
	public class ApplicationFocusListener : MonoBehaviour
	{
		private static ApplicationFocusListener _instance;

		public static ApplicationFocusListener Instance
		{
			get
			{
				if (_instance != null) return _instance;
				var go = new GameObject("[Enjoylix] ApplicationFocusListener");
				DontDestroyOnLoad(go);
				_instance = go.AddComponent<ApplicationFocusListener>();
				return _instance;
			}
		}

		public event Action<bool> FocusChanged;

		private void OnApplicationFocus(bool hasFocus)
		{
			FocusChanged?.Invoke(hasFocus);
		}
	}
}
