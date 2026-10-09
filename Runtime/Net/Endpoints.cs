namespace EnjoylixSDK.Net
{
	internal static class Endpoints
	{
		public static class Auth
		{
			public const string LoginPage = "/login"; 
			public const string RegisterPage = "/register";
			public const string LinkGuestPage = "/link-guest";

			public const string GuestLogin = "/api/v2/auth/{project_name}/guest_login";
			public const string UserInfo = "/api/v1/users/me";
			public const string CheckVerificationNeeded = "/api/v1/age_verification/{project_name}/check_needed";
			public const string RequestVerification = "/age-verification";

			public const string GetTokensPair = "/api/v2/auth/{project_name}/token";
			public const string RefreshToken = "/api/v2/auth/refresh";
			public const string GameSession = "/api/v2/auth/game_session";
		}

		public static class Attribution
		{
			public const string SendEvent = "/ingame/{project_name}/event";
		}
	}
}