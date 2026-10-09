using System;

namespace EnjoylixSDK.Purchase.Models
{
	[Serializable]
	public class InitTransactionRequest
	{
		public string lot_string_id;
	}

	[Serializable]
	public class InitTransactionResponse
	{
		public string datastore_id;
	}
	
}