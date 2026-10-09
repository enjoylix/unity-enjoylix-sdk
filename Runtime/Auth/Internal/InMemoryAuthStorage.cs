namespace EnjoylixSDK.Auth.Internal
{
	public class InMemoryAuthStorage : IAuthStorage
	{
		private string _guestId;
		private string _accessToken;
		private string _refreshToken;
		private string _deviceId;
		private bool? _isGuest;

		public string LoadGuestId() => _guestId;
		public void SaveGuestId(string guestId) => _guestId = guestId;

		public string LoadAccessToken() => _accessToken;
		public void SaveAccessToken(string token) => _accessToken = token;

		public string LoadRefreshToken() => _refreshToken;
		public void SaveRefreshToken(string token) => _refreshToken = token;

		public string LoadDeviceId() => _deviceId;
		public void SaveDeviceId(string deviceId) => _deviceId = deviceId;

		public bool? LoadIsGuest() => _isGuest;
		public void SaveIsGuest(bool isGuest) => _isGuest = isGuest;

		public void Clear()
		{
			_guestId = null;
			_accessToken = null;
			_refreshToken = null;
			_deviceId = null;
			_isGuest = null;
		}
	}
}