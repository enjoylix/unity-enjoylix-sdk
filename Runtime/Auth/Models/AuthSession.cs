using System;

namespace EnjoylixSDK.Auth.Models
{
	[Serializable]
	public class AuthSession
	{
		public string accessToken;
		public OpenIdUserResponse me;
	}
}