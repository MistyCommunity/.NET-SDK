using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MistyRobotics.SDK.Logger;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;
using Windows.Storage;

namespace MistyRobotics.ExternalCallSkill.ServicesManagement
{
	/// <summary>
	/// Wrapper class for Azure Cognitive Services
	/// </summary>
	internal class AzureCognitiveService
	{
		private ISDKLogger _logger;
		private SpeechConfig _speechConfig;
		private SpeechTranslationConfig _speechTranslationConfig;
		private ComputerVisionClient _computerVisionClient;
		private SemaphoreSlim _computerVisionSemaphore = new SemaphoreSlim(1, 1);
		private SemaphoreSlim _speechSemaphore = new SemaphoreSlim(1, 1);
		private Random _randomGenerator = new Random();
		private IList<AzureServiceType> _availableServices = new List<AzureServiceType>();
		private IDictionary<AzureServiceType, AzureServiceAuthorization> _servicesAuthorization = new Dictionary<AzureServiceType, AzureServiceAuthorization>();

		public string CurrentSpeakingVoice { get; set; } = "en-AU-HayleyRUS";
		public AzureProfanitySetting AzureProfanitySetting { get; set; } = AzureProfanitySetting.Raw;

		public AzureCognitiveService(IDictionary<AzureServiceType, AzureServiceAuthorization> servicesAuthorization, ISDKLogger logger)
		{
			_logger = logger;
			_servicesAuthorization = servicesAuthorization;

			foreach(KeyValuePair<AzureServiceType, AzureServiceAuthorization> auth in _servicesAuthorization)
			{
				if(auth.Key == AzureServiceType.ComputerVision)
				{
					_computerVisionClient = new ComputerVisionClient(
						new ApiKeyServiceClientCredentials(auth.Value.SubscriptionKey),
						new System.Net.Http.DelegatingHandler[] { });
					_computerVisionClient.Endpoint = auth.Value.Endpoint;

					_availableServices.Add(AzureServiceType.ComputerVision);
				}
				else if (auth.Key == AzureServiceType.Speech)
				{
					_speechConfig = SpeechConfig.FromSubscription(auth.Value.SubscriptionKey, auth.Value.Region);
					_speechTranslationConfig = SpeechTranslationConfig.FromSubscription(auth.Value.SubscriptionKey, auth.Value.Region);
					SetProfanityOption(AzureProfanitySetting);
					_speechTranslationConfig.SpeechSynthesisVoiceName = CurrentSpeakingVoice;
					
					_availableServices.Add(AzureServiceType.Speech);

				}
			}
		}

		/// <summary>
		/// Map the profanity setting to the proper option in the config
		/// </summary>
		/// <param name="azureProfanitySetting"></param>
		private void SetProfanityOption(AzureProfanitySetting azureProfanitySetting)
		{
			switch (azureProfanitySetting)
			{
				case AzureProfanitySetting.Removed:
					_speechTranslationConfig.SetProfanity(ProfanityOption.Removed);
					_speechConfig.SetProfanity(ProfanityOption.Removed);
					break;
				case AzureProfanitySetting.Masked:
					_speechTranslationConfig.SetProfanity(ProfanityOption.Masked);
					_speechConfig.SetProfanity(ProfanityOption.Masked);
					break;
				case AzureProfanitySetting.Raw:
				default:
					_speechTranslationConfig.SetProfanity(ProfanityOption.Raw);
					_speechConfig.SetProfanity(ProfanityOption.Raw);
					break;
			}
		}

		/// <summary>
		/// Using an audio stream, get the translation of that audio file
		/// </summary>
		/// <param name="audioData"></param>
		/// <param name="fromLanguage"></param>
		/// <param name="toLanguages"></param>
		/// <returns></returns>
		public async Task<TranslationRecognitionResult> TranslateAudioStream(byte[] audioData, string fromLanguage, IList<string> toLanguages)
		{
			if (!_availableServices.Contains(AzureServiceType.Speech))
			{
				return null;
			}

			_speechSemaphore.Wait();
			try
			{
				TranslationRecognitionResult result;

				StorageFolder localFolder = ApplicationData.Current.LocalFolder;

				//TODO Update to use PullAudioInputStream
				StorageFile storageFile = await localFolder.CreateFileAsync("AudioFromStream.wav", CreationCollisionOption.ReplaceExisting);
				using (var stream = await storageFile.OpenStreamForWriteAsync())
				{
					await stream.WriteAsync(audioData, 0, audioData.Count());
					stream.Close();
				}

				var audioConfig = AudioConfig.FromWavFileInput(storageFile.Path);
				_speechTranslationConfig.SpeechRecognitionLanguage = fromLanguage;

				foreach (string language in toLanguages)
				{
					_speechTranslationConfig.AddTargetLanguage(language);
				}
				
				using (var translationRecognizer = new TranslationRecognizer(_speechTranslationConfig, audioConfig))
				{
					result = await translationRecognizer.RecognizeOnceAsync();
				}
				
				if (result.Reason == ResultReason.Canceled)
				{
					var cancellation = CancellationDetails.FromResult(result);
					_logger.LogWarning($"Call cancelled.  {cancellation.Reason}");

					if (cancellation.Reason == CancellationReason.Error)
					{
						_logger.Log($"Cancel error code = {cancellation.ErrorCode}");
						_logger.Log($"Cancel details = {cancellation.ErrorDetails}");

						if (cancellation.ErrorCode == CancellationErrorCode.NoError)
						{
							_logger.Log("You may be having an authorization issue, are your keys correct and up to date?");

						}
					}
				}
				else if (result.Reason == ResultReason.TranslatedSpeech)
				{
					_logger.Log($"Azure Translation. '{result.Reason}': {result.Text}");
				}
				return result;
			}
			catch (Exception ex)
			{
				string message = "Failed processing image.";
				_logger.Log(message, ex);
				return null;
			}
			finally
			{
				_speechSemaphore.Release();
			}
		}

		/// <summary>
		/// Using an audio file in either the LocalFolder (or the Assets folder for testing)
		/// get the translation of that audio file
		/// </summary>
		/// <param name="filename"></param>
		/// <param name="fromLanguage"></param>
		/// <param name="toLanguages"></param>
		/// <param name="useAssetFolder"></param>
		/// <returns></returns>
		public async Task<TranslationRecognitionResult> TranslateAudioFile(string filename, string fromLanguage, IList<string> toLanguages, bool useAssetFolder = false)
		{
			if (!_availableServices.Contains(AzureServiceType.Speech))
			{
				return null;
			}

			_speechSemaphore.Wait();
			try
			{
				TranslationRecognitionResult result;

				StorageFolder localFolder;
				if (!useAssetFolder)
				{
					localFolder = ApplicationData.Current.LocalFolder;
				}
				else
				{
					localFolder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets");
				}

				StorageFile file = await localFolder.GetFileAsync(filename);

				var audioConfig = AudioConfig.FromWavFileInput(file.Path);
				_speechTranslationConfig.SpeechRecognitionLanguage = fromLanguage;
				
				foreach (string language in toLanguages)
				{
					_speechTranslationConfig.AddTargetLanguage(language);
				}
				
				using (var translationRecognizer = new TranslationRecognizer(_speechTranslationConfig, audioConfig))
				{
					result = await translationRecognizer.RecognizeOnceAsync();
				}
				
				if (result.Reason == ResultReason.Canceled)
				{
					var cancellation = CancellationDetails.FromResult(result);
					_logger.LogWarning($"Call cancelled.  {cancellation.Reason}");

					if (cancellation.Reason == CancellationReason.Error)
					{
						_logger.Log($"Cancel error code = {cancellation.ErrorCode}");
						_logger.Log($"Cancel details = {cancellation.ErrorDetails}");

						if (cancellation.ErrorCode == CancellationErrorCode.NoError)
						{
							_logger.Log("You may be having an authorization issue, are your keys correct and up to date?");

						}
					}
				}
				else if (result.Reason == ResultReason.TranslatedSpeech)
				{
					_logger.Log($"Azure Translation. '{result.Reason}': {result.Text}");
				}
				return result;
			}
			catch (Exception ex)
			{
				string message = "Failed processing image.";
				_logger.Log(message, ex);
				return null;
			}
			finally
			{
				_speechSemaphore.Release();
			}
		}

		/// <summary>
		/// Analyze the image and return a description
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		public async Task<string> AnalyzeImage(string url)
		{
			if (!_availableServices.Contains(AzureServiceType.ComputerVision))
			{
				return null;
			}

			_computerVisionSemaphore.Wait();
			try
			{
				ImageDescription imageDescription = await _computerVisionClient.DescribeImageAsync(url, 1);
				if (!imageDescription.Captions.Any())
				{
					return "I have no idea.";
				}
				else
				{
					return imageDescription.Captions.First().Text;
				}
			}
			catch (Exception ex)
			{
				string message = "Failed processing image.";
				_logger.Log(message, ex);
				return message;
			}
			finally
			{
				_computerVisionSemaphore.Release();
			}
		}

		/// <summary>
		/// Return an audio file for the passed in text
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public async Task<byte[]> TextToSpeechFile(string text)
		{
			if (!_availableServices.Contains(AzureServiceType.Speech))
			{
				return null;
			}

			_speechSemaphore.Wait();
			try
			{
				StorageFolder localFolder = ApplicationData.Current.LocalFolder;

				//TODO Update to use PullAudioInputStream
				StorageFile storageFile = await localFolder.CreateFileAsync("TTSAudio.wav", CreationCollisionOption.ReplaceExisting);
				
				if (!string.IsNullOrWhiteSpace(CurrentSpeakingVoice))
				{
					_speechConfig.SpeechSynthesisVoiceName = CurrentSpeakingVoice;
				}

				SetProfanityOption(AzureProfanitySetting);
				
				using (var fileOutput = AudioConfig.FromWavFileOutput(storageFile.Path))
				{
					using (var synthesizer = new SpeechSynthesizer(_speechConfig, fileOutput))
					{
						var result = await synthesizer.SpeakTextAsync(text);
						if (result.Reason == ResultReason.Canceled)
						{
							var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
							_logger.LogWarning($"Call cancelled.  {cancellation.Reason}");

							if (cancellation.Reason == CancellationReason.Error)
							{
								_logger.Log($"Cancel error code = {cancellation.ErrorCode}");
								_logger.Log($"Cancel details = {cancellation.ErrorDetails}");

								if (cancellation.ErrorCode == CancellationErrorCode.NoError)
								{
									_logger.Log("You may be having an authorization issue, are your keys correct and up to date?");
								}
							}
							return null;
						}

						_logger.Log($"Audio Received. '{result.Reason}'");						
						return result.AudioData;
					}
				}
			}
			catch (Exception ex)
			{
				string message = "Failed processing text to speech.";
				_logger.Log(message, ex);
				return null;
			}
			finally
			{
				_speechSemaphore.Release();
			}
		}

		/// <summary>
		/// Analyze the image and return a description
		/// </summary>
		/// <param name="stream"></param>
		/// <returns></returns>
		public async Task<string> AnalyzeImage(Stream stream)
		{
			if (!_availableServices.Contains(AzureServiceType.ComputerVision))
			{
				return null;
			}

			_computerVisionSemaphore.Wait();
			try
			{
				ImageDescription imageDescription = await _computerVisionClient.DescribeImageInStreamAsync(stream);
				if (!imageDescription.Captions.Any())
				{
					return "I have no idea.";
				}
				else
				{
					return imageDescription.Captions.First().Text;
				}
			}
			catch (Exception ex)
			{
				string message = "Failed processing image.";
				_logger.Log(message, ex);
				return message;
			}
			finally
			{
				_computerVisionSemaphore.Release();
			}
		}
	}
}