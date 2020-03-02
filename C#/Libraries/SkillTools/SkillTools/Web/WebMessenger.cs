using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace SkillTools.Web
{
	/// <summary>
	/// Simple Http class to communicate with the outside world
	/// </summary>
	public class WebMessenger
	{
		private const string LoggingStartString = "Misty Robotics [••] WebMessenger : ";

		/// <summary>
		/// Method to make a GET request to an external endpoint
		/// </summary>
		/// <param name="endpoint">the endpoint to call</param>
		/// <returns></returns>
		public IAsyncOperation<WebMessengerData> GetRequest(string endpoint)
		{
			return GetInternalRequest(endpoint).AsAsyncOperation();
		}
		
		private async Task<WebMessengerData> GetInternalRequest(string endpoint)
		{
			StreamReader readStream = null;
			HttpWebResponse response = null;
			string responseString = "";

			try
			{
				HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);

				// Set some reasonable limits on resources used by this request
				//request.MaximumAutomaticRedirections = 4;
				//request.MaximumResponseHeadersLength = 4;

				// Set credentials to use for this request.
				request.Credentials = CredentialCache.DefaultCredentials;
				response = (HttpWebResponse)await request.GetResponseAsync();
				
				Stream receiveStream = response.GetResponseStream();				
				readStream = new StreamReader(receiveStream, Encoding.UTF8);

				responseString = readStream.ReadToEnd();
				return new WebMessengerData { Response = responseString, HttpCode = (int)(response?.StatusCode ?? HttpStatusCode.InternalServerError) };
			}
			catch (WebException ex)
			{
				string dateTimeLogString = $"{LoggingStartString} {DateTime.UtcNow.ToString("MM/dd/yy hh:mm:ss.fff tt")}";
				HttpStatusCode errorCode = ((HttpWebResponse)ex.Response)?.StatusCode ?? HttpStatusCode.InternalServerError;
				responseString = $"WebMessenger failed to connect to GET endpoint '{endpoint}' - Received status code: {errorCode} ";
				Console.WriteLine($"{dateTimeLogString} {responseString}");
				return new WebMessengerData { Response = responseString, HttpCode = (int)errorCode };
			}
			catch (Exception ex)
			{
				string dateTimeLogString = $"{LoggingStartString} {DateTime.UtcNow.ToString("MM/dd/yy hh:mm:ss.fff tt")}";
				responseString = $"{dateTimeLogString} WebMessenger failed to connect to GET endpoint '{endpoint}' - Exception:{ex.Message}";
				Console.WriteLine(responseString);
				return new WebMessengerData { Response = responseString, HttpCode = (int)HttpStatusCode.InternalServerError };
			}
			finally
			{
				response?.Dispose();
				readStream?.Dispose();
			}
		}

		/// <summary>
		/// Method to make an http POST request to an external endpoint
		/// </summary>
		/// <param name="endpoint">the endpoint to call</param>
		/// <param name="data">data to send</param>
		/// <param name="contentType">the request content type</param>
		/// <returns></returns>
		public IAsyncOperation<WebMessengerData> PostRequest(string endpoint, string data, string contentType)
		{
			return MakeRequest(endpoint, data, "POST", contentType).AsAsyncOperation();
		}

		/// <summary>
		/// Method to make an http DELETE request to an external endpoint
		/// </summary>
		/// <param name="endpoint">the endpoint to call</param>
		/// <param name="data">data to send</param>
		/// <param name="contentType">the request content type</param>
		/// <returns></returns>
		public IAsyncOperation<WebMessengerData> DeleteRequest(string endpoint, string data, string contentType)
		{
			return MakeRequest(endpoint, data,  "DELETE", contentType).AsAsyncOperation();
		}

		/// <summary>
		/// Method to make an http PATCH request to an external endpoint
		/// </summary>
		/// <param name="endpoint">the endpoint to call</param>
		/// <param name="data">data to send</param>
		/// <param name="contentType">the request content type</param>
		/// <returns></returns>
		public IAsyncOperation<WebMessengerData> PatchRequest(string endpoint, string data, string contentType)
		{
			return MakeRequest(endpoint, data, "PATCH", contentType).AsAsyncOperation();
		}

		/// <summary>
		/// Method to make an http PUT request to an external endpoint
		/// </summary>
		/// <param name="endpoint">the endpoint to call</param>
		/// <param name="data">data to send</param>
		/// <param name="contentType">the request content type</param>
		/// <returns></returns>
		public IAsyncOperation<WebMessengerData> PutRequest(string endpoint, string data, string contentType)
		{
			return MakeRequest(endpoint, data, "PUT", contentType).AsAsyncOperation();
		}

		private async Task<WebMessengerData> MakeRequest(string endpoint, string data, string requestMethod, string contentType)
		{
			WebResponse response = null;
			Stream dataStream = null;
			string responseString = "";

			requestMethod = string.IsNullOrWhiteSpace(requestMethod) ? "POST" : requestMethod;
			data = string.IsNullOrWhiteSpace(data) ? "{}" : data;
			contentType = string.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType;
			try
			{
				// Create a request using a URL that can receive a post.   
				WebRequest request = WebRequest.Create(endpoint);
				request.Method = requestMethod;
				request.Credentials = CredentialCache.DefaultCredentials;

				byte[] byteArray = Encoding.UTF8.GetBytes(data);

				// Set the ContentType property of the WebRequest.  
				request.ContentType = contentType;
				// Set the ContentLength property of the WebRequest.  
				//request.ContentLength = byteArray.Length;

				dataStream = await request.GetRequestStreamAsync();
				dataStream.Write(byteArray, 0, byteArray.Length);
				// Close the Stream object.  
				dataStream.Close();

				// Get the response.  
				response = (HttpWebResponse)await request.GetResponseAsync();
				HttpStatusCode responseCode = ((HttpWebResponse)response)?.StatusCode ?? HttpStatusCode.InternalServerError;

				using (dataStream = response.GetResponseStream())
				{
					StreamReader reader = new StreamReader(dataStream);
					responseString = reader.ReadToEnd();
				}

				return new WebMessengerData { Response = responseString, HttpCode = (int)responseCode };
			}
			catch (WebException ex)
			{
				string dateTimeLogString = $"{LoggingStartString} {DateTime.UtcNow.ToString("MM/dd/yy hh:mm:ss.fff tt")}";
				HttpStatusCode errorCode = ((HttpWebResponse)ex.Response)?.StatusCode ?? HttpStatusCode.InternalServerError;
				responseString = $"WebMessenger failed to connect to {requestMethod} endpoint '{endpoint}' using {contentType} content type - Received status code: {errorCode} ";
				Console.WriteLine($"{dateTimeLogString} {responseString}");
				return new WebMessengerData { Response = responseString, HttpCode = (int)errorCode };
			}
			catch (Exception ex)
			{
				string dateTimeLogString = $"{LoggingStartString} {DateTime.UtcNow.ToString("MM/dd/yy hh:mm:ss.fff tt")}";
				responseString = $"WebMessenger failed to connect to {requestMethod} endpoint '{endpoint}' using {contentType} content type - Exception:{ex.Message}";
				Console.WriteLine($"{dateTimeLogString} {responseString}");
				return new WebMessengerData { Response = responseString, HttpCode = (int)HttpStatusCode.InternalServerError };
			}
			finally
			{
				response?.Dispose();
				dataStream?.Dispose();
			}
		}
	}
}
