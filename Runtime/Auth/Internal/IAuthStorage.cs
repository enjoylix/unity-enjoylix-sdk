namespace EnjoylixSDK.Auth.Internal
{
	public interface IAuthStorage
	{
		string LoadGuestId();
		void SaveGuestId(string guestId);

		string LoadAccessToken();
		void SaveAccessToken(string token);
		string LoadRefreshToken();
		void SaveRefreshToken(string token);

		void Clear();
		void SaveDeviceId(string deviceId);
		string LoadDeviceId();

		bool? LoadIsGuest();
		void SaveIsGuest(bool isGuest);
	}
}