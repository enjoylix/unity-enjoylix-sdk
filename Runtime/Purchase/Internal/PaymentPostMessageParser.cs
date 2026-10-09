using System;
using UnityEngine;

namespace EnjoylixSDK.Purchase.Internal
{
	internal static class PaymentPostMessageParser
	{
		public static bool TryParse(string json, out bool success, out string paymentId, out string gameTransactionId)
		{
			success = false;
			paymentId = null;
			gameTransactionId = null;

			if (string.IsNullOrEmpty(json))
				return false;

			try
			{
				var dto = JsonUtility.FromJson<PaymentPostMessageDto>(json);
				if (dto == null)
					return false;

				if (!string.Equals(dto.type, "payment_result", StringComparison.OrdinalIgnoreCase))
					return false;

				success = dto.success;

				paymentId = dto.payment_id;
				
				gameTransactionId = dto.game_transaction_id;

				return !string.IsNullOrEmpty(paymentId);
			}
			catch(Exception e)
			{
				Debug.LogError("[PaymentPostMessageParser] Parse failed: " + e + "\nJSON: " + json);
				return false;
			}
		}
	}
}