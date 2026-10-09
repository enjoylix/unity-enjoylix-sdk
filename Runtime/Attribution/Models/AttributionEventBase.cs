using System;

namespace EnjoylixSDK.Attribution.Models
{
	[Serializable]
	public class AttributionEventBase
	{
		public long timestamp;     // unix seconds
		public string player_id;
		public string auth_id;
		public string partner;
		public string platform;
		public string @event;
		public string event_data_json;
	}
}