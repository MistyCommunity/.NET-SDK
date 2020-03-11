namespace CognitiveServicesExample.ServicesManagement
{
	/// <summary>
	/// Subscription authorization information
	/// </summary>
	public sealed class AzureServiceAuthorization
	{
		public AzureServiceType ServiceType { get; set; }
		public string SubscriptionKey { get; set; }
		public string Region { get; set; }
		public string Endpoint { get; set; }
	}
}
