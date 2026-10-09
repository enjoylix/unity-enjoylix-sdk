using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace EnjoylixSDK.Auth.Internal
{
	public class PlayerPrefsAuthStorage : IAuthStorage
	{
		private const string GuestIdKey = "enjoylix_guest_id";
		private const string AccessTokenKey = "enjoylix_access_token";
		private const string RefreshTokenKey = "enjoylix_refresh_token";
		private const string DeviceIdKey = "enjoylix_device_id";
		private const string IsGuestKey = "enjoylix_is_guest";

		private readonly byte[] _encryptionKey;

		public PlayerPrefsAuthStorage(string encryptionSalt)
		{
			_encryptionKey = GenerateDeviceBoundKey(encryptionSalt);
		}

		public string LoadGuestId()
			=> PlayerPrefs.GetString(GuestIdKey, null);

		public void SaveGuestId(string guestId)
		{
			if (!string.IsNullOrEmpty(guestId))
				PlayerPrefs.SetString(GuestIdKey, guestId);
		}

		public string LoadAccessToken()
			=> PlayerPrefs.GetString(AccessTokenKey, null);

		public void SaveAccessToken(string token)
		{
			if (!string.IsNullOrEmpty(token))
				PlayerPrefs.SetString(AccessTokenKey, token);
		}

		public string LoadRefreshToken()
		{
			var encryptedTokenBase64 = PlayerPrefs.GetString(RefreshTokenKey, null);
			if (string.IsNullOrEmpty(encryptedTokenBase64)) return null;

			try
			{
				return DecryptString(encryptedTokenBase64);
			}
			catch (Exception e)
			{
				Debug.LogWarning($"Failed to decrypt refresh token: {e.Message}");
				return null;
			}
		}

		public void SaveRefreshToken(string token)
		{
			if (string.IsNullOrEmpty(token))
			{
				PlayerPrefs.DeleteKey(RefreshTokenKey);
			}
			else
			{
				PlayerPrefs.SetString(RefreshTokenKey, EncryptString(token));
			}
		}

		public void SaveDeviceId(string deviceId)
		{
			if (!string.IsNullOrEmpty(deviceId))
				PlayerPrefs.SetString(DeviceIdKey, deviceId);
		}
		public string LoadDeviceId()
			=> PlayerPrefs.GetString(DeviceIdKey, null);

		public bool? LoadIsGuest() 
		{
			if (!PlayerPrefs.HasKey(IsGuestKey))
				return null;
			return PlayerPrefs.GetInt(IsGuestKey) == 1;
		}

		public void SaveIsGuest(bool isGuest)
			=> PlayerPrefs.SetInt(IsGuestKey, isGuest ? 1 : 0);

		public void Clear()
		{
			PlayerPrefs.DeleteKey(GuestIdKey);
			PlayerPrefs.DeleteKey(AccessTokenKey);
			PlayerPrefs.DeleteKey(RefreshTokenKey);
			PlayerPrefs.DeleteKey(IsGuestKey);
		}

		#region Encoding

		private byte[] GenerateDeviceBoundKey(string salt)
		{
			string rawData = SystemInfo.deviceUniqueIdentifier + Application.identifier + salt;

			using (var sha256 = SHA256.Create())
			{
				return sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
			}
		}

		private string EncryptString(string plainText)
		{
			using Aes aes = Aes.Create();
			aes.Key = _encryptionKey;
			aes.GenerateIV();

			using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
			using var ms = new MemoryStream();

			ms.Write(aes.IV, 0, aes.IV.Length);

			using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
			using (var sw = new StreamWriter(cs))
			{
				sw.Write(plainText);
			}
			return Convert.ToBase64String(ms.ToArray());
		}

		private string DecryptString(string cipherTextBase64)
		{
			byte[] fullCipher = Convert.FromBase64String(cipherTextBase64);
			using Aes aes = Aes.Create();

			byte[] iv = new byte[aes.BlockSize / 8];
			Array.Copy(fullCipher, 0, iv, 0, iv.Length);
			aes.Key = _encryptionKey;
			aes.IV = iv;

			using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
			using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
			using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
			using var sr = new StreamReader(cs);

			return sr.ReadToEnd();
		}

		#endregion
	}
}