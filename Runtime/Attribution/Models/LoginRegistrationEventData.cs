using System;

namespace EnjoylixSDK.Attribution.Models
{
	[Serializable]
	public class LoginRegistrationEventData
	{
		public string player_country;
		public string platform_id;
		public string client_ip;
		public string query_params_json; // JSON string
		public string user_agent;
		public string forwarded_for;
		public string referer;
	}
}