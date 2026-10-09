using System;

namespace EnjoylixSDK.Purchase.Internal
{
	[Serializable]
	public sealed class PaymentPostMessageDto
	{
		public string type;
		public bool success;
		public string payment_id;
		public string game_transaction_id;
	}
}