namespace EnjoylixSDK.Attribution
{
	public interface IAttributionService
	{
		void TrackRegistration();
		void TrackLogin();
		void StartSessionPing();
		void TrackPing();
		void StopPing();
		void TrackPurchase(string eventType, string source, string checkoutId, string transactionId, float amount, string currencyCode, bool isQaPurchase);
		void TrackTutorialComplete();
	}
}