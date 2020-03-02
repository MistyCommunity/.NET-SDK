using Windows.Foundation;

namespace SkillTools.Web
{
	/// <summary>
	/// Simple Http class to communicate with the outside world
	/// </summary>
	public interface IWebMessenger
	{
		/// <summary>
		/// Method to make a GET request to an external endpoint
		/// </summary>
		/// <param name="endpoint">the endpoint to call</param>
		/// <returns></returns>
		IAsyncOperation<WebMessengerData> GetRequest(string endpoint);

		/// <summary>
		/// Method to make an http POST request to an external endpoint
		/// </summary>
		/// <param name="endpoint">the endpoint to call</param>
		/// <param name="data">data to send</param>
		/// <param name="contentType">the request content type</param>
		/// <returns></returns>
		IAsyncOperation<WebMessengerData> PostRequest(string endpoint, string data, string contentType);

		/// <summary>
		/// Method to make an http DELETE request to an external endpoint
		/// </summary>
		/// <param name="endpoint">the endpoint to call</param>
		/// <param name="data">data to send</param>
		/// <param name="contentType">the request content type</param>
		/// <returns></returns>
		IAsyncOperation<WebMessengerData> DeleteRequest(string endpoint, string data, string contentType);

		/// <summary>
		/// Method to make an http PATCH request to an external endpoint
		/// </summary>
		/// <param name="endpoint">the endpoint to call</param>
		/// <param name="data">data to send</param>
		/// <param name="contentType">the request content type</param>
		/// <returns></returns>
		IAsyncOperation<WebMessengerData> PatchRequest(string endpoint, string data, string contentType);

		/// <summary>
		/// Method to make an http PUT request to an external endpoint
		/// </summary>
		/// <param name="endpoint">the endpoint to call</param>
		/// <param name="data">data to send</param>
		/// <param name="contentType">the request content type</param>
		/// <returns></returns>
		IAsyncOperation<WebMessengerData> PutRequest(string endpoint, string data, string contentType);
	}
}