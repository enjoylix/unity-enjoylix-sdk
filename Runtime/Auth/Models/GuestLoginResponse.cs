namespace EnjoylixSDK.Auth.Models
{
	public class GuestLoginResponse
	{
		public string id;
		public bool is_guest;
		public bool confirmed;
		public bool age_verified;
		public string access_token;
		public string refresh_token;
	}
}