namespace SkillTools.Web
{
	/// <summary>
	/// Response from http request
	/// </summary>
	public sealed class WebMessengerData
	{
		/// <summary>
		/// Http response code
		/// </summary>
		public int HttpCode { get; set; }

		/// <summary>
		/// Response as text
		/// </summary>
		public string Response { get; set; }

	}
}
