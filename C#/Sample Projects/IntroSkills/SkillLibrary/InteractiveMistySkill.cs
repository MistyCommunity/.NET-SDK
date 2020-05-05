using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MistyRobotics.Common.Data;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Responses;

namespace SkillLibrary
{
	/**********************************************************************
		Copyright 2020 Misty Robotics
		Licensed under the Apache License, Version 2.0 (the "License");
		you may not use this file except in compliance with the License.
		You may obtain a copy of the License at
			http://www.apache.org/licenses/LICENSE-2.0
		Unless required by applicable law or agreed to in writing, software
		distributed under the License is distributed on an "AS IS" BASIS,
		WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
		See the License for the specific language governing permissions and
		limitations under the License.

		**WARRANTY DISCLAIMER.**

		* General. TO THE MAXIMUM EXTENT PERMITTED BY APPLICABLE LAW, MISTY
		ROBOTICS PROVIDES THIS SAMPLE SOFTWARE "AS-IS" AND DISCLAIMS ALL
		WARRANTIES AND CONDITIONS, WHETHER EXPRESS, IMPLIED, OR STATUTORY,
		INCLUDING THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
		PURPOSE, TITLE, QUIET ENJOYMENT, ACCURACY, AND NON-INFRINGEMENT OF
		THIRD-PARTY RIGHTS. MISTY ROBOTICS DOES NOT GUARANTEE ANY SPECIFIC
		RESULTS FROM THE USE OF THIS SAMPLE SOFTWARE. MISTY ROBOTICS MAKES NO
		WARRANTY THAT THIS SAMPLE SOFTWARE WILL BE UNINTERRUPTED, FREE OF VIRUSES
		OR OTHER HARMFUL CODE, TIMELY, SECURE, OR ERROR-FREE.
		* Use at Your Own Risk. YOU USE THIS SAMPLE SOFTWARE AND THE PRODUCT AT
		YOUR OWN DISCRETION AND RISK. YOU WILL BE SOLELY RESPONSIBLE FOR (AND MISTY
		ROBOTICS DISCLAIMS) ANY AND ALL LOSS, LIABILITY, OR DAMAGES, INCLUDING TO
		ANY HOME, PERSONAL ITEMS, PRODUCT, OTHER PERIPHERALS CONNECTED TO THE PRODUCT,
		COMPUTER, AND MOBILE DEVICE, RESULTING FROM YOUR USE OF THIS SAMPLE SOFTWARE
		OR PRODUCT.

		Please refer to the Misty Robotics End User License Agreement for further
		information and full details:
			https://www.mistyrobotics.com/legal/end-user-license-agreement/
	**********************************************************************/

	/// <summary>
	/// Skill to demonstrate basic event registration, unregistration and handling
	/// Also shows the different ways a user can listen for events/callbacks
	/// Assumes defaults assets are on the robot, update the names of assets if they have changed
	/// </summary>
	public class InteractiveMistySkill : IMistySkill
	{
		/// <summary>
		/// Robot reference
		/// </summary>
		private IRobotMessenger _misty;

		/// <summary>
		/// Flag indicating class was disposed
		/// </summary>
		private bool _disposed = false;

		/// <summary>
		/// Random generator class for generating random movements
		/// </summary>
		private Random _randomGenerator = new Random();

		/// <summary>
		/// List of images on the robot
		/// </summary>
		private IList<ImageDetails> _imageList { get; set; } = new List<ImageDetails>();

		/// <summary>
		/// List of audio files on the robot
		/// </summary>
		private IList<AudioDetails> _audioList { get; set; } = new List<AudioDetails>();
		

		#region Required Skill Methods, Event Handlers & Accessors
		
		/// <summary>
		/// Skill details for the robot
		/// Currently you need a guid to distinguish your skill from others on the robot, get one at this link and paste it in
		/// https://www.guidgenerator.com/online-guid-generator.aspx
		/// 
		/// There are other parameters you can set if you want:
		///   Description - a description of your skill
		///   TimeoutInSeconds - timeout of skill in seconds
		///   StartupRules - a list of options to indicate if a skill should start immediately upon startup
		///   BroadcastMode - different modes can be set to share different levels of information from the robot using the 'SkillData' websocket
		///   AllowedCleanupTimeInMs - How long to wait after calling OnCancel before denying messages from the skill and performing final cleanup  
		/// </summary>
		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("InteractiveMistySkill", "3946e40c-ac22-4426-b349-3c0de391ac9b")
		{
			TimeoutInSeconds = 60 * 5 //runs for 5 minutes or until the skill is cancelled
		};

		/// <summary>
		///	This method is called by the wrapper to set your robot interface
		///	You need to save this off in the local variable commented on above as you are going use it to call the robot
		/// </summary>
		/// <param name="robotInterface"></param>
		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
		}

		/// <summary>
		/// Get the assets and startup some timers to do random movements and other things...
		/// </summary>
		/// <param name="parameters"></param>
		public async void OnStart(object sender, IDictionary<string, object> parameters)
		{
			try
			{
				var deviceInfo = await _misty.GetDeviceInformationAsync();

				if (!_misty.Wait(0)) { return; }

				//Get the audio and image lists for use in the skill
				_audioList = (await _misty.GetAudioListAsync())?.Data;

				if (!_misty.Wait(0)) { return; }
				_imageList = (await _misty.GetImageListAsync())?.Data;


				if (!_misty.Wait(0)) { return; }

				if (_audioList != null && _audioList.Count > 0)
				{
					AudioDetails randomAudio = _audioList[_randomGenerator.Next(0, _audioList.Count - 1)];
					_misty.PlayAudio(randomAudio.Name, 100, null);
				}

				if (_imageList != null && _imageList.Count > 0)
				{
					ImageDetails randomImage = _imageList[_randomGenerator.Next(0, _imageList.Count - 1)];
					_misty.DisplayImage(randomImage.Name, 1, null);
				}


				if (!_misty.Wait(2000)) { return; }

				_misty.ChangeLED(255, 255, 255, null);

				//Register a number of events
				_misty.RegisterAudioPlayCompleteEvent(AudioPlayCallback, 0, true,null, null);				
				_misty.RegisterCapTouchEvent(CapTouchCallback, 0, true, null, null, null);
				_misty.RegisterKeyPhraseRecognizedEvent(KeyPhraseRecognizedCallback, 250, true, null, null);
				_misty.StartKeyPhraseRecognition(null);
				_misty.StartFaceRecognition(null);
				
				//Create an event with a specific name so we can unregister it when needed using that name
				_misty.RegisterBumpSensorEvent(BumpCallback, 0, true, null, "MyBumpSensorName", null);

				//Register face rec with keepAlive = false, it will need to be reregistered after triggering if the user wants it to run again
				_misty.RegisterFaceRecognitionEvent(FaceRecCallback, 0, false, null, null, null);

				//Play audio indicator that the event registration state has changed
				if (_audioList != null && _audioList.Count > 0)
				{
					AudioDetails randomAudio = _audioList[_randomGenerator.Next(0, _audioList.Count - 1)];
					_misty.PlayAudio(randomAudio.Name, 100, null);
					await Task.Delay(1000);
					_misty.PlayAudio(randomAudio.Name, 100, null);
				}

				if (!_misty.Wait(30000)) { return; }


				//Unregister the bump sensor
				_misty.UnregisterEvent("MyBumpSensorName", UnregisterCallback);

				//Play audio indicator that the event registration state has changed
				if (_audioList != null && _audioList.Count > 0)
				{
					AudioDetails randomAudio = _audioList[_randomGenerator.Next(0, _audioList.Count - 1)];
					_misty.PlayAudio(randomAudio.Name, 100, null);
					await Task.Delay(1000);
					_misty.PlayAudio(randomAudio.Name, 100, null);
				}

				if (!_misty.Wait(20000)) { return; }

				//Unregister ALL events
				_misty.UnregisterAllEvents(UnregisterCallback);

				//Play audio indicator that the event registration state has changed
				if (_audioList != null && _audioList.Count > 0)
				{
					AudioDetails randomAudio = _audioList[_randomGenerator.Next(0, _audioList.Count - 1)];
					_misty.PlayAudio(randomAudio.Name, 100, null);
					await Task.Delay(1000);
					_misty.PlayAudio(randomAudio.Name, 100, null);
				}

				
				if (!_misty.Wait(20000)) { return; }

				//Play audio indicator that the event registration state has changed
				if (_audioList != null && _audioList.Count > 0)
				{
					AudioDetails randomAudio = _audioList[_randomGenerator.Next(0, _audioList.Count - 1)];
					_misty.PlayAudio(randomAudio.Name, 100, null);
					await Task.Delay(1000);
					_misty.PlayAudio(randomAudio.Name, 100, null);
				}

				//Re-register events 
				_misty.RegisterAudioPlayCompleteEvent(AudioPlayCallback, 0, true, null, null);
				_misty.RegisterFaceRecognitionEvent(FaceRecCallback, 0, false, null, null, null);
				_misty.RegisterCapTouchEvent(CapTouchCallback, 0, true, null, null, null);
				_misty.RegisterKeyPhraseRecognizedEvent(KeyPhraseRecognizedCallback, 0, true, null, null);

				//You can also register events without callbacks, it requires a user to subscribe to that event as follows...
				//Note that this re-registers bump events to play a sound on release, not contact as they were previously handled
				_misty.RegisterBumpSensorEvent(0, true, null, null, null);
				_misty.BumpSensorEventReceived += ProcessBumpEvent;

				//Will continue to process events until timeout of cancel
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log($"InteractiveMistySkill : OnStart: => Exception", ex);
			}
		}

		/// <summary>
		/// Example Unregister Command callback
		/// </summary>
		/// <param name="response"></param>
		private void UnregisterCallback(IRobotCommandResponse response)
		{
			_misty.SkillLogger.Log($"InteractiveMistySkill : UnregisterCallback called");
		}

		/// <summary>
		/// Handle the cancel command sent from the robot
		/// </summary>
		/// <param name="parameters"></param>
		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			_misty.SkillLogger.LogVerbose($"InteractiveMistySkill : OnCancel called");
			DoCleanup();
			_misty.ChangeLED(255, 0, 0, null);
		}

		/// <summary>
		/// Called when the skill times out
		/// </summary>
		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			_misty.SkillLogger.LogVerbose($"InteractiveMistySkill : OnTimeout called");
			DoCleanup();
			_misty.ChangeLED(0, 0, 255, null);
		}

		/// <summary>
		/// OnPause simply calls OnCancel in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Pause is not implemented by default and it simply calls OnCancel
			_misty.SkillLogger.LogVerbose($"InteractiveMistySkill : OnPause called");
			OnCancel(sender, parameters);
		}

		/// <summary>
		/// OnResume simply calls OnStart in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Resume is not implemented by default and it simply calls OnStart
			_misty.SkillLogger.LogVerbose($"InteractiveMistySkill : OnResume called");
			OnStart(sender, parameters);
		}

		/// <summary>
		/// Dispose method must be implemented by the class
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Protected Dispose implementation
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}

			if (disposing)
			{
				// Free any other managed objects here.
			}

			// Free any unmanaged objects here.
			_disposed = true;
		}

		/// <summary>
		/// Skill Finalizer/Destructor
		/// </summary>
		/// <param name="disposing"></param>
		~InteractiveMistySkill()
		{
			Dispose(false);
		}
		
		#endregion

		#region User Created Helper Methods

		/// <summary>
		/// Performs some cleanup on the state of the robot
		/// </summary>
		private void DoCleanup()
		{
			_misty?.StopKeyPhraseRecognition(null);
			_misty?.StopFaceRecognition(null);
		}

		#endregion

		#region User Created Callbacks

		/// <summary>
		/// Callback is called when an audio file complated playing
		/// </summary>
		/// <param name="robotEvent"></param>
		private void AudioPlayCallback(IAudioPlayCompleteEvent robotEvent)
		{
			_misty.SkillLogger.Log($"InteractiveMistySkill : AudioPlayCallback called for audio file {(robotEvent).Name}");
		}

		/// <summary>
		/// Callback called when the bump sensor is contacted or released
		/// This method only process IsContacted events
		/// </summary>
		/// <param name="bumpEvent"></param>
		private void BumpCallback(IBumpSensorEvent bumpEvent)
		{
			if (bumpEvent.IsContacted)
			{
				_misty.Stop(null);
				switch (bumpEvent.SensorPosition)
				{
					case BumpSensorPosition.FrontRight:
						_misty.PlayAudio("s_PhraseHello.wav", 100, null);
						break;
					case BumpSensorPosition.FrontLeft:
						_misty.PlayAudio("s_PhraseUhOh.wav", 100, null);
						break;
					case BumpSensorPosition.BackRight:
						_misty.PlayAudio("s_Love.wav", 100, null);
						break;
					case BumpSensorPosition.BackLeft:
						_misty.PlayAudio("s_Boredom.wav", 100, null);
						break;
				}
			}
		}

		/// <summary>
		/// Callback called when a cap touch sensor is contacted or released
		/// This method only process IsContacted events
		/// </summary>
		/// <param name="capTouchEvent"></param>
		private void CapTouchCallback(ICapTouchEvent capTouchEvent)
		{

			if (capTouchEvent.IsContacted)
			{
				switch (capTouchEvent.SensorPosition)
				{
					case CapTouchPosition.Back:
						_misty.PlayAudio("s_Love.wav", 100, null);
						_misty.DisplayImage("e_Love.jpg", 1, null);
						break;
					case CapTouchPosition.Front:
						_misty.PlayAudio("s_Amazement.wav", 100, null);
						_misty.DisplayImage("e_Amazement.jpg", 1, null);
						break;
					case CapTouchPosition.Right:
						_misty.PlayAudio("Joy.wav", 100, null);
						_misty.DisplayImage("JoyGoofy3.jpg", 1, null);
						break;
					case CapTouchPosition.Left:
						_misty.PlayAudio("s_Fear.wav", 100, null);
						_misty.DisplayImage("e_Terror.jpg", 1, null);
						break;
					case CapTouchPosition.Scruff:
						_misty.PlayAudio("s_Rage.wav", 100, null);
						_misty.DisplayImage("e_Rage4.jpg", 1, null);
						break;
					case CapTouchPosition.Chin:
						_misty.PlayAudio("s_Sleepy.wav", 100, null);
						_misty.DisplayImage("e_Sleepy2.jpg", 1, null);
						break;
				}
			}
		}

		/// <summary>
		/// Callback called when the voice recognition "Hey Misty" event is triggered
		/// </summary>
		/// <param name="voiceEvent"></param>
		private void KeyPhraseRecognizedCallback(IKeyPhraseRecognizedEvent voiceEvent)
		{

			_misty.DisplayImage("e_Love.jpg", 1, null);
			_misty.PlayAudio("s_Awe.wav", 100, null);

			if (!_misty.Wait(3000)) { return; }
			_misty.StartKeyPhraseRecognition(null);
		}

		/// <summary>
		/// Callback called when a face is detected or recognized
		/// </summary>
		/// <param name="faceRecEvent"></param>
		private void FaceRecCallback(IFaceRecognitionEvent faceRecEvent)
		{
			if (faceRecEvent.Label == "unknown person")
			{
				AudioDetails randomAudio = _audioList[_randomGenerator.Next(0, _audioList.Count - 1)];
				ImageDetails randomImage = _imageList[_randomGenerator.Next(0, _imageList.Count - 1)];
				_misty.DisplayImage(randomImage.Name, 1, null);
				_misty.PlayAudio(randomAudio.Name, 100, null);
			}
			else
			{
				_misty.DisplayImage("e_EcstacyStarryEyed.jpg", 1, null);
				_misty.PlayAudio("sEcstacy.wav", 100, null);
			}

			if (!_misty.Wait(5000)) { return; }
			_misty.DisplayImage("e_ContentLeft.jpg", 1, null);
			if (!_misty.Wait(5000)) { return; }
			_misty.RegisterFaceRecognitionEvent(FaceRecCallback, 0, false, null, null, null);
		}

		#endregion

		#region User Created Event Handler Example

		/// <summary>
		/// Example of using an event instead of a callback for the bump event
		/// This call plays audio when the bumper is released, not contacted
		/// </summary>
		/// <param name="bumpEvent"></param>
		private void ProcessBumpEvent(object sender, IBumpSensorEvent bumpEvent)
		{
			if (!bumpEvent.IsContacted)
			{
				_misty.Stop(null);
				switch (bumpEvent.SensorPosition)
				{
					case BumpSensorPosition.FrontRight:
						_misty.PlayAudio("s_PhraseHello.wav", 100, null);
						break;
					case BumpSensorPosition.FrontLeft:
						_misty.PlayAudio("s_PhraseUhOh.wav", 100, null);
						break;
					case BumpSensorPosition.BackRight:
						_misty.PlayAudio("s_Love.wav", 100, null);
						break;
					case BumpSensorPosition.BackLeft:
						_misty.PlayAudio("s_Boredom.wav", 100, null);
						break;
				}
			}
		}

		#endregion
	}
}