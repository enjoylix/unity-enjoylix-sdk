using System;

namespace EnjoylixSDK.Attribution.Models
{
	[Serializable]
	public class PurchaseEventData
	{
		public string source;
		public string checkout_id;
		public string transaction_id;
		public float amount;
		public string currency_code;
		public bool is_qa_purchase;
	}
}