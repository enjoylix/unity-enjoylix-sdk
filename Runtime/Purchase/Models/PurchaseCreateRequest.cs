using System;

namespace EnjoylixSDK.Purchase.Models
{
	[Serializable]
	public sealed class PurchaseCreateRequest
	{
		public int coin_amount;
		public string game_name;
		public string transaction_id;
		public string transaction_image_url;
		public string transaction_name;
	}

	[Serializable]
	public sealed class PurchaseCreateResponse
	{
		public string transaction_url;
	}

	[Serializable]
	public sealed class PurchaseCompleteRequest
	{
		public string payment_id;
	}

	[Serializable]
	public sealed class PurchaseCheckoutRequest
	{
		public string payment_method;
		public string bundle_id;
		public string payway;
	}

	[Serializable]
	public sealed class PurchaseStatusResponse
	{
		public string id;
		public string created_on;
		public string status;
	}

	[Serializable]
	public sealed class CoinSpendStatusResponse
	{
		public string id;
		public string status;
	}
	
	[Serializable]
	public sealed class PaymentResult
	{
		public bool success;
		public string payment_id;
		public string transaction_id;
		
		public string raw;
		public string error;
	}
}