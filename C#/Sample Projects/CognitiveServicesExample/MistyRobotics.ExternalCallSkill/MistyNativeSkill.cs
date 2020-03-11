using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MistyRobotics.Common.Data;
using MistyRobotics.ExternalCallSkill.ServicesManagement;
using MistyRobotics.SDK.Responses;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.Tools.DataStorage;
using MistyRobotics.SDK.Events;
using MistyRobotics.Common.Types;
using Microsoft.CognitiveServices.Speech.Translation;
using Newtonsoft.Json;
using SkillTools.AssetTools;

namespace MistyRobotics.ExternalCallSkill
{
	/// <summary>
	/// Skill to show examples of using Azure Cognitive Services with Misty
	/// </summary>
	public sealed class MistyNativeSkill : IMistySkill
	{
		/// <summary>
		/// Simple key value file store
		/// </summary>
		private ISkillStorage _skillStorage;

		/// <summary>
		/// Simple helper class encapsulating system assets with 
		/// basic display image and play audio calls
		/// ** Assumes a certain asset set is installed on the robot **
		/// </summary>
		private IAssetWrapper _assetWrapper;

		/// <summary>
		/// Interface into azure calls
		/// </summary>
		private AzureCognitiveService _azureCognitive;

		/// <summary>
		/// Voice for Default speaking 
		/// Can be overridden with StartupParameter "DefaultVoice"
		/// </summary>
		private string _defaultVoice = "en-AU-HayleyRUS";

		/// <summary>
		/// From language for default and "repeat after me" speaking
		/// Can be overridden with StartupParameter "FromDefaultLanguage"
		/// </summary>
		private string _fromDefaultLanguage = "en-US";

		/// <summary>
		/// To language for default and "repeat after me" translations
		/// Can be overridden with StartupParameter "ToDefaultLanguage"
		/// </summary>
		private string _toDefaultLanguage = "en";

		/// <summary>
		/// Voice for foreign translations, voice to use for translated code
		/// This should match an appropriate _toForeignLanguage for better pronuniciation
		/// Can be overridden with StartupParameter "ForeignVoice"
		/// </summary>
		private string _foreignVoice = "es-MX-Raul-Apollo";

		/// <summary>
		/// From language for foreign translations, the speaker's lanaguage
		/// Can be overridden with StartupParameter "FromForeignLanguage"
		/// </summary>
		private string _fromForeignLanguage = "en-US";

		/// <summary>
		/// To language for foreign translations
		/// This should match an appropriate _foreignVoice for better pronuniciation
		/// Can be overridden with StartupParameter "ToForeignLanguage"
		/// </summary>
		private string _toForeignLanguage = "es-MX";

		/// <summary>
		/// A local variable to hold the misty robot interface
		/// </summary>
		private IRobotMessenger _misty;

		/// <summary>
		/// Face recognition on/off flag
		/// </summary>
		private bool _faceRecognitionProcessingOn = false;

		/// <summary>
		/// Skill details for the robot		
		/// </summary>
		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("ExternalCallSkill", "8f9abb90-b558-4458-8e11-1b67de1e0d42")
		{
			TimeoutInSeconds = int.MaxValue,
			AllowedCleanupTimeInMs = 6000,
			StartupRules = new List<NativeStartupRule> { NativeStartupRule.Manual, NativeStartupRule.Startup }
		};

		/// <summary>
		///	Method is called by the wrapper to set your robot interface
		/// </summary>
		/// <param name="robotInterface"></param>
		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
		}
		
		/// <summary>
		/// This event handler is called when the robot/user sends a start message
		/// The parameters can be set in the Skill Runner (or as json) and used in the skill if desired
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="parameters"></param>
		public async void OnStart(object sender, IDictionary<string, object> parameters)
		{
			try
			{
				await InitializeSkill();
				await ProcessStartupParameters(parameters);
				await ProcessAdjustableParameters(parameters);
				
				_misty.ChangeLED(255, 0, 0, null);
				if (!_misty.Wait(0)) { return; }
			
				//Spanish translations
				List<BumpSensorValidation> backLeftBumpValidations = new List<BumpSensorValidation>();
				backLeftBumpValidations.Add(new BumpSensorValidation(BumpSensorFilter.SensorName, ComparisonOperator.Equal, BumpSensorPosition.BackLeft));
				backLeftBumpValidations.Add(new BumpSensorValidation(BumpSensorFilter.IsContacted, ComparisonOperator.Equal, true));
				_misty.RegisterBumpSensorEvent(ProcessBackLeftBumpEvent, 250, true, backLeftBumpValidations, null, null);
			
				//Face Rec on/off toggle
				List<BumpSensorValidation> backRightBumpValidations = new List<BumpSensorValidation>();
				backRightBumpValidations.Add(new BumpSensorValidation(BumpSensorFilter.SensorName, ComparisonOperator.Equal, BumpSensorPosition.BackRight));
				backRightBumpValidations.Add(new BumpSensorValidation(BumpSensorFilter.IsContacted, ComparisonOperator.Equal, true));
				_misty.RegisterBumpSensorEvent(ProcessBackRightBumpEvent, 250, true, backRightBumpValidations, null, null);

				//Picture description
				List<BumpSensorValidation> frontRightBumpValidations = new List<BumpSensorValidation>();
				frontRightBumpValidations.Add(new BumpSensorValidation(BumpSensorFilter.SensorName, ComparisonOperator.Equal, BumpSensorPosition.FrontRight));
				frontRightBumpValidations.Add(new BumpSensorValidation(BumpSensorFilter.IsContacted, ComparisonOperator.Equal, true));
				_misty.RegisterBumpSensorEvent(ProcessFrontRightBumpEvent, 250, true, frontRightBumpValidations, null, null);
			
				//Repeat
				List<BumpSensorValidation> frontLeftBumpValidations = new List<BumpSensorValidation>();
				frontLeftBumpValidations.Add(new BumpSensorValidation(BumpSensorFilter.SensorName, ComparisonOperator.Equal, BumpSensorPosition.FrontLeft));
				frontLeftBumpValidations.Add(new BumpSensorValidation(BumpSensorFilter.IsContacted, ComparisonOperator.Equal, true));
				_misty.RegisterBumpSensorEvent(ProcessFrontLeftBumpEvent, 250, true, frontLeftBumpValidations, null, null);

				RegisterUserEvents();

				if (!_misty.Wait(2000)) { return; }

				//All ready to go
				_assetWrapper.PlaySystemSound(SystemSound.Amazement);
				_assetWrapper.ShowSystemImage(SystemImage.EcstacyStarryEyed);
				_misty.ChangeLED(0, 0, 255, null);

				if (!_misty.Wait(1500)) { return; }

				BroadcastDetails("Hello!  How are you?", _defaultVoice);
				await _misty.TransitionLEDAsync(255, 0, 0, 0,0, 255, LEDTransition.TransitOnce, 1000);

				/*	
				//Test code to describe a random image at this URL
				BroadcastDetails("Describing random image.");
				_misty.Wait(500);			
				string description = await _azureCognitive.AnalyzeImage("http://junglebiscuit.com/images/random/rand_image.pl");
				BroadcastDetails(description);
				*/
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log("Failed while starting the skill.", ex);
			}
		}

		/// <summary>
		/// This event handler is called when Pause is called on the skill
		/// User can save the skill status/data to be retrieved when Resume is called
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="parameters"></param>
		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			OnCancel(sender, parameters);
		}
		
		/// <summary>
		/// This event handler is called when Resume is called on the skill
		/// User can restore any skill status/data and continue from Paused location
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="parameters"></param>
		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			OnStart(sender, parameters);
		}

		/// <summary>
		/// This event handler is called when the cancel command is issued from the robot/user
		/// You currently have the time designated in AllowedCleanupTimeInMs to do cleanup and robot resets before the skill is shut down... 
		/// Events will be unregistered for you 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="parameters"></param>
		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			EndSkill();
		}

		/// <summary>
		/// This event handler is called when the skill timeouts
		/// You currently have the time designated in AllowedCleanupTimeInMs to do cleanup and robot resets before the skill is shut down... 
		/// Events will be unregistered for you 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="parameters"></param>
		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			EndSkill();
		}

		/// <summary>
		/// Method to load the startup parameters into the skill fields
		/// </summary>
		/// <param name="parameters"></param>
		private async Task ProcessStartupParameters(IDictionary<string, object> parameters)
		{
			try
			{
				IDictionary<string, object> storedData = await _skillStorage.LoadDataAsync() ?? new Dictionary<string, object>();

				int numRuns = 0;
				if (storedData.ContainsKey("NumberOfRuns"))
				{
					numRuns = Convert.ToInt32(storedData["NumberOfRuns"]);
					storedData.Remove("NumberOfRuns");
				}
				storedData.Add("NumberOfRuns", ++numRuns);

				//Handle Passed in auth settings. 
				//TODO Should encypt this info for production
				IDictionary<AzureServiceType, AzureServiceAuthorization> servicesAuth = new Dictionary<AzureServiceType, AzureServiceAuthorization>();

				string region = "westus";
				string endpoint = "https://westus.api.cognitive.microsoft.com/";

				string key = GetStringField(parameters, storedData, "visionkey");
				if (key != null)
				{
					region = GetStringField(parameters, storedData, "visionregion") ?? "westus";
					endpoint = GetStringField(parameters, storedData, "visionendpoint") ?? "https://westus.api.cognitive.microsoft.com/";

					servicesAuth.Remove(AzureServiceType.ComputerVision);
					servicesAuth.Add
					(
						AzureServiceType.ComputerVision,
						new AzureServiceAuthorization
						{
							Region = region,
							Endpoint = endpoint,
							ServiceType = AzureServiceType.ComputerVision,
							SubscriptionKey = key
						}
					);
				}

				key = GetStringField(parameters, storedData, "speechkey");
				if (key != null)
				{
					region = GetStringField(parameters, storedData, "speechregion") ?? "westus";
					endpoint = GetStringField(parameters, storedData, "speechendpoint") ?? "https://westus.api.cognitive.microsoft.com/";

					servicesAuth.Remove(AzureServiceType.Speech);
					servicesAuth.Add
					(
						AzureServiceType.Speech,
						new AzureServiceAuthorization
						{
							Region = region,
							Endpoint = endpoint,
							ServiceType = AzureServiceType.Speech,
							SubscriptionKey = key
						}
					);
				}
				_azureCognitive = new AzureCognitiveService(servicesAuth, _misty.SkillLogger);

				await _skillStorage.SaveDataAsync(storedData);

			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log("Failed handling startup parameters", ex);
			}
		}

		/// <summary>
		/// Say, log and send websocket events for the message
		/// </summary>
		/// <param name="message"></param>
		/// <param name="voice"></param>
		private async void BroadcastDetails(string message, string voice = null, bool useBuiltInTTS = false)
		{
			if (useBuiltInTTS)
			{
				await _misty.SpeakAsync(message, true, null);
				return;
			}
			try
			{
				_misty.SkillLogger.Log(message);
				_misty.PublishMessage(message, null);

				if (!string.IsNullOrWhiteSpace(voice))
				{
					_azureCognitive.CurrentSpeakingVoice = voice;
				}

				var audioData = await _azureCognitive.TextToSpeechFile(message);
				if (audioData == null || audioData.Count() == 0)
				{
					//Azure failed, use onboard TTS
					await _misty.SpeakAsync(message, true, null);
				}
				else
				{
					await _misty.SaveAudioAsync("TTSFile.wav", audioData, true, true);
				}
				
			}
			catch(Exception ex)
			{
				_misty.SkillLogger.Log("Failed to speak and broadcast details.", ex);
			}
		}

		/// <summary>
		/// User RESTful event processor for Describe command
		/// </summary>
		/// <param name="userEvent"></param>
		private async void ProcessDescribeEvent(IUserEvent userEvent)
		{
			await Describe();
		}

		/// <summary>
		/// Register the User Trigerred RESTful events
		/// </summary>
		private void RegisterUserEvents()
		{
			//Setup User events that can be called from RESTful calls to trigger events or update settings
			_misty.RegisterUserEvent("Describe", ProcessDescribeEvent, 0, true, null);
			_misty.RegisterUserEvent("Repeat", ProcessRepeatEvent, 0, true, null);
			_misty.RegisterUserEvent("Update", ProcessUpdateEvent, 0, true, null);
			_misty.RegisterUserEvent("Translate", ProcessTranslateEvent, 0, true, null);
			_misty.RegisterUserEvent("Speak", ProcessSpeakEvent, 0, true, null);

			/*
			 * 
			Can call these from postman
			POST  <robot_ip>/api/skills/event

			Setup Payload Body as JSON
			
			//Take a picture and describe the scene
			{
 				"Skill": "8f9abb90-b558-4458-8e11-1b67de1e0d42",
 				"EventName": "Describe",
				"Payload" : { }
			 }

			//Listen for 5 seconds and repeat
			{
 				"Skill": "8f9abb90-b558-4458-8e11-1b67de1e0d42",
 				"EventName": "Repeat",
				"Payload" : { }
			 }

			//Update settings payload example
			 {
 				"Skill": "8f9abb90-b558-4458-8e11-1b67de1e0d42",
 				"EventName": "Update",
				"Payload" : {
					"DefaultVoice": "en-US-BenjaminRUS"
 				}
			 }

			//Listen for 5 seconds and translate
			{
 				"Skill": "8f9abb90-b558-4458-8e11-1b67de1e0d42",
 				"EventName": "Translate",
				"Payload" : { }
			 }

			//Text to Speech - will translate the text sent
			{
 				"Skill": "8f9abb90-b558-4458-8e11-1b67de1e0d42",
 				"EventName": "Speak",
				"Payload" : {
					"Text": "What to say."
 				}
			 }
			*/
		}

		/// <summary>
		/// User RESTful event processor for Update command
		/// </summary>
		/// <param name="userEvent"></param>
		private async void ProcessUpdateEvent(IUserEvent userEvent)
		{
			try
			{
				IDictionary<string, object> payloadData = JsonConvert.DeserializeObject<Dictionary<string, object>>(userEvent.Data["Payload"].ToString());
				await ProcessAdjustableParameters(payloadData);
			}
			catch(Exception ex)
			{
				_misty.SkillLogger.Log("Unable to process payload in Update call.  Please check your payload configuration.", ex);
			}
		}

		/// <summary>
		/// User RESTful event processor for Repeat command
		/// </summary>
		/// <param name="userEvent"></param>
		private async void ProcessRepeatEvent(IUserEvent userEvent)
		{
			await Repeat();
		}

		/// <summary>
		/// User RESTful event processor for Translate command
		/// </summary>
		/// <param name="userEvent"></param>
		private void ProcessTranslateEvent(IUserEvent userEvent)
		{
			Translate();
		}

		/// <summary>
		/// User RESTful event processor for Speak command
		/// </summary>
		/// <param name="userEvent"></param>
		private void ProcessSpeakEvent(IUserEvent userEvent)
		{
			try
			{
				IDictionary<string, object> payloadData = JsonConvert.DeserializeObject<Dictionary<string, object>>(userEvent.Data["Payload"].ToString());
				
				KeyValuePair<string, object> textKVP = payloadData.FirstOrDefault(x => x.Key.ToLower().Trim() == "text");
				if (textKVP.Value != null)
				{
					BroadcastDetails(Convert.ToString(textKVP.Value), _defaultVoice);					
				}
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log("Unable to process payload in Update call.  Please check your payload configuration.", ex);
			}			
		}

		/// <summary>
		/// Process Face Recognition events
		/// Should only be called if FaceRec is on
		/// </summary>
		/// <param name="faceRecEvent"></param>
		private async void ProcessFaceRecognitionEvent(IFaceRecognitionEvent faceRecEvent)
		{
			try
			{ 
				string label = faceRecEvent.Label.ToLower();

				ITakePictureResponse takePictureResponse = await _misty.TakePictureAsync(label, false, true, true, null, null);
			
				if (label == "unknown person")
				{
					Stream stream = new MemoryStream((byte[])takePictureResponse.Data.Image);
					string description = await _azureCognitive.AnalyzeImage(stream);
					BroadcastDetails($"I see a person, {(string.IsNullOrWhiteSpace(description) ? "but I cannot describe them." : description)}", _defaultVoice);
				}
				else
				{
					BroadcastDetails($"Hello, {label}!", _defaultVoice);
				}

				//Wait a bit so we don't flood with face events, how about 10 seconds?
				_misty.Wait(10000);
				_misty.RegisterFaceRecognitionEvent(ProcessFaceRecognitionEvent, 100, false, null, null, null);
				_assetWrapper.ShowSystemImage(SystemImage.Amazement);
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log("Failed to process the face recognition event.", ex);
			}
		}
		
		/// <summary>
		/// Listen and perform the translation using foreign voice and language fields
		/// </summary>
		private async void Translate()
		{
			try
			{
				_assetWrapper.PlaySystemSound(SystemSound.SystemWakeWord);
				_assetWrapper.ShowSystemImage(SystemImage.Joy);
				await _misty.StartRecordingAudioAsync("TranslateAzureAudio.wav");
				_misty.Wait(5000);

				await _misty.StopRecordingAudioAsync();
				_assetWrapper.PlaySystemSound(SystemSound.SystemSuccess);
				_misty.Wait(500);

				IGetAudioResponse audioResponse = await _misty.GetAudioAsync("TranslateAzureAudio.wav", false);

				TranslationRecognitionResult description = await _azureCognitive.TranslateAudioStream((byte[])audioResponse.Data.Audio, _fromForeignLanguage, new List<string> { _toForeignLanguage });
				if (description.Translations.Any())
				{
					string translation = description.Translations.FirstOrDefault(x => x.Key == _toForeignLanguage).Value;

					//Sometimes comes back without region
					if (string.IsNullOrWhiteSpace(translation))
					{
						string toLanguage2 = _toForeignLanguage.Split('-')[0];
						translation = description.Translations.FirstOrDefault(x => x.Key == toLanguage2).Value;
					}

					if (string.IsNullOrWhiteSpace(translation))
					{
						BroadcastDetails("I am unable to translate this.", _defaultVoice);
					}
					else
					{
						BroadcastDetails(translation, _foreignVoice);
					}
				}

				_assetWrapper.ShowSystemImage(SystemImage.ContentRight);
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log("Failed to translate the audio input.", ex);
			}
		}

		/// <summary>
		/// Process the Back Left Bump event
		/// </summary>
		/// <param name="userEvent"></param>
		private void ProcessBackLeftBumpEvent(IBumpSensorEvent userEvent)
		{
			Translate();
		}

		/// <summary>
		/// Toggle face rec on and off
		/// </summary>
		private void ToggleFaceRec()
		{
			if (!_faceRecognitionProcessingOn)
			{
				_misty.RegisterFaceRecognitionEvent(ProcessFaceRecognitionEvent, 100, false, null, "FaceRec", null);
				_misty.StartFaceRecognition(null);
				_faceRecognitionProcessingOn = true;
			}
			else
			{
				_misty.UnregisterEvent("FaceRec", null);
				_misty.StopFaceRecognition(null);
				_faceRecognitionProcessingOn = false;
			}

			BroadcastDetails($"Turned Face Recognition {(_faceRecognitionProcessingOn ? "On" : "Off")}", _defaultVoice);

		}

		/// <summary>
		/// Process back right bump event
		/// </summary>
		/// <param name="userEvent"></param>
		private void ProcessBackRightBumpEvent(IBumpSensorEvent userEvent)
		{
			ToggleFaceRec();
		}

		/// <summary>
		/// Listen and perform the repeat using default voice and language fields
		/// </summary>
		/// <returns></returns>
		private async Task Repeat()
		{
			try
			{
				_assetWrapper.ShowSystemImage(SystemImage.JoyGoofy2);
				_assetWrapper.PlaySystemSound(SystemSound.SystemWakeWord);
				await _misty.StartRecordingAudioAsync("DefaultAzureAudio.wav");
				_misty.Wait(5000);

				await _misty.StopRecordingAudioAsync();
				_assetWrapper.PlaySystemSound(SystemSound.SystemSuccess);
				_misty.Wait(500);

				//Test for mic issue... this uses a file vs. recording
				//TranslationRecognitionResult description = await _azureCognitive.TranslateAudioFile("TestMe.wav", "en-US", new List<string> { "en" }, true);

				//Else use recorded audio
				IGetAudioResponse audioResponse = await _misty.GetAudioAsync("DefaultAzureAudio.wav", false);
				TranslationRecognitionResult description = await _azureCognitive.TranslateAudioStream((byte[])audioResponse.Data.Audio, _fromDefaultLanguage, new List<string> { _toDefaultLanguage });
				BroadcastDetails($"You said {(description?.Text == null ? "something, but I failed to process it." : $",{description.Text}")}", _defaultVoice);

				_assetWrapper.ShowSystemImage(SystemImage.ContentLeft);
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log("Failed to repeat the audio input.", ex);
			}
		}

		/// <summary>
		/// Process the front left bump event
		/// </summary>
		/// <param name="bumpEvent"></param>
		private async void ProcessFrontLeftBumpEvent(IBumpSensorEvent bumpEvent)
		{
			await Repeat();
		}

		/// <summary>
		/// Process the front right bump event
		/// </summary>
		/// <param name="bumpEvent"></param>
		private async void ProcessFrontRightBumpEvent(IBumpSensorEvent bumpEvent)
		{
			await Describe();
		}

		/// <summary>
		/// Take a picture and describe what you see using default voice and language settings
		/// </summary>
		/// <returns></returns>
		private async Task Describe()
		{
			try
			{
				_assetWrapper.ShowSystemImage(SystemImage.SystemCamera);
				_misty.Wait(2000);
				_assetWrapper.PlaySystemSound(SystemSound.SystemCameraShutter);
				//Take pic and analyze
				ITakePictureResponse takePictureResponse = await _misty.TakePictureAsync("FrontRightBumpImage", false, true, true, null, null);
				Stream stream = new MemoryStream((byte[])takePictureResponse.Data.Image);
				string description = await _azureCognitive.AnalyzeImage(stream);
				BroadcastDetails($"{(string.IsNullOrWhiteSpace(description) ? "I cannot process the image." : $"Looks like, { description}.")}", _defaultVoice);
				_misty.Wait(5000);
				_assetWrapper.ShowSystemImage(SystemImage.DefaultContent);
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log("Failed to describe the picture.", ex);
			}
		}

		/// <summary>
		/// Initialize the startup parameters, database, etc
		/// </summary>
		/// <param name="parameters"></param>
		private async Task InitializeSkill()
		{
			//If the skill was stopped while in debugging mode before cancel was called
			//These could still be registered... unregister them now just in case
			_misty.UnregisterAllEvents(null);
			await _misty.StopFaceRecognitionAsync();
			await _misty.StopFaceDetectionAsync();
			
			_skillStorage = SkillStorage.GetDatabase(Skill);
			_assetWrapper = new AssetWrapper(_misty);			
		}

		/// <summary>
		/// Helper method to get the field from the db or parameter string
		/// </summary>
		/// <param name="parameters"></param>
		/// <param name="storedData"></param>
		/// <param name="dataKey"></param>
		/// <returns></returns>
		private string GetStringField(IDictionary<string, object> parameters, IDictionary<string, object> storedData, string dataKey)
		{
			string newValue = null;
			KeyValuePair<string, object> dataKVP = parameters.FirstOrDefault(x => x.Key.ToLower().Trim() == dataKey.ToLower());
			if (dataKVP.Value != null)
			{
				newValue = Convert.ToString(dataKVP.Value);
				storedData.Remove(dataKey);
				storedData.Add(dataKey, newValue ?? "unknown");
			}
			else if (storedData.ContainsKey(dataKey))
			{
				newValue = Convert.ToString(storedData[dataKey]);
			}
			return newValue;
		}


		/// <summary>
		/// Method to load the adjustable parameters into the skill fields
		/// </summary>
		/// <param name="parameters"></param>
		private async Task ProcessAdjustableParameters(IDictionary<string, object> parameters)
		{
			try
			{
				IDictionary<string, object> storedData = await _skillStorage.LoadDataAsync() ?? new Dictionary<string, object>();

				_defaultVoice = GetStringField(parameters, storedData, "defaultvoice") ?? _defaultVoice;
				_fromDefaultLanguage = GetStringField(parameters, storedData, "fromdefaultlanguage") ?? _fromDefaultLanguage;
				_toDefaultLanguage = GetStringField(parameters, storedData, "todefaultlanguage") ?? _toDefaultLanguage;
				_foreignVoice = GetStringField(parameters, storedData, "foreignvoice") ?? _foreignVoice;
				_fromForeignLanguage = GetStringField(parameters, storedData, "fromforeignlanguage") ?? _fromForeignLanguage;
				_toForeignLanguage = GetStringField(parameters, storedData, "toforeignlanguage") ?? _toForeignLanguage;
				
				string profanityString = GetStringField(parameters, storedData, "profanitysetting");
				if (!string.IsNullOrWhiteSpace(profanityString) &&  Enum.TryParse(typeof(AzureProfanitySetting), Convert.ToString(profanityString.Trim()), true, out object profanitySetting))
				{
					_azureCognitive.AzureProfanitySetting = (AzureProfanitySetting)profanitySetting;
				}
				//Handle passed in an audio volume, otherwise should use current default and not a stored value
				KeyValuePair<string, object> volumeKVP = parameters.FirstOrDefault(x => x.Key.ToLower().Trim() == "volume");
				if (volumeKVP.Value != null)
				{
					int volume = Convert.ToInt32(volumeKVP.Value);
					if (volume >= 0 && volume <= 100)
					{
						await _misty.SetDefaultVolumeAsync(volume);
					}
				}
				await _skillStorage.SaveDataAsync(storedData);
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log("Failed handling startup parameters", ex);
			}
		}
		
		/// <summary>
		/// End skill actions
		/// </summary>
		private void EndSkill()
		{
			_misty.SkillLogger.LogInfo("EndSkill called.");
			_misty.SendDebugMessage("EndSkill called.", null);
			_misty.Stop(null);

			_assetWrapper.ShowSystemImage(SystemImage.SleepingZZZ);
			
			BroadcastDetails("Goodbye.", _defaultVoice);
			
			_misty.ChangeLED(255, 0, 0, null);
		}
		
		#region IDisposable Support

		private bool _isDisposed = false;

		private void Dispose(bool disposing)
		{
			if (!_isDisposed)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects).
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				_isDisposed = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~MistyNativeSkill() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}
