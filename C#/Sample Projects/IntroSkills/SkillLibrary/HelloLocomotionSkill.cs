using System;
using System.Collections.Generic;
using System.Threading;
using MistyRobotics.SDK.Events;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.Common.Data;
using MistyRobotics.SDK.Messengers;

namespace SkillLibrary
{
	/// <summary>
	/// Skill to demonstrate simple driving and the hazards
	/// Also adds most of the previous skills' functionality to give the robot a fuller personality
	/// Assumes defaults assets are on the robot, update the names of assets if they have changed
	/// </summary>
	public class HelloLocomotionSkill : IMistySkill
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
		/// Random Generator for the move head, change led, and move arm values
		/// </summary>
		private Random _randomGenerator = new Random();

		/// <summary>
		/// Timer object to perform callbacks at a interval to process the data and do the next action
		/// </summary>
		private Timer _heartbeatTimer;

		/// <summary>
		/// Timer object to perform callbacks at a regular interval to move the head
		/// </summary>
		private Timer _moveHeadTimer;

		/// <summary>
		/// Timer object to perform callbacks at a regular interval to move the arms
		/// </summary>
		private Timer _moveArmsTimer;

		/// <summary>
		/// Timer object to perform callbacks at a regular interval to change the LED
		/// </summary>
		private Timer _ledTimer;

		/// <summary>
		/// Flag to indicate the direction the robot is moving
		/// </summary>
		private bool _goingForward = false;

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
		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("HelloLocomotionSkill", "600ea0a9-e5f7-433d-88f5-492f72e343ca")
		{
			TimeoutInSeconds = 60 * 5  //runs for 5 minutes or until the skill is cancelled
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
				//Get the audio and image lists for use in the skill
				_audioList = (await _misty.GetAudioListAsync())?.Data;
				_imageList = (await _misty.GetImageListAsync())?.Data;
				
				_misty.Wait(2000);
				
				_misty.PlayAudio("s_Awe.wav", 100, null); 
				_misty.ChangeLED(255, 255, 255, null);
				_misty.DisplayImage("e_ContentRight.jpg", 1, null);

				_misty.StartKeyPhraseRecognition(null);
				_misty.StartFaceRecognition(null);
				RegisterEvents();

				_heartbeatTimer = new Timer(HeartbeatCallback, null, 3000, 3000);
				_moveHeadTimer = new Timer(MoveHeadCallback, null, 5000, 10000);
				_moveArmsTimer = new Timer(MoveArmCallback, null, 5000, 4000);
				_ledTimer = new Timer(ChangeLEDCallback, null, 2000, 2000);
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log($"HelloLocomotionSkill : OnStart: => Exception", ex);
			}
		}

		/// <summary>
		/// Called when the skill is cancelled
		/// </summary>
		/// <param name="parameters"></param>
		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			_misty.SkillLogger.LogVerbose($"HelloLocomotionSkill : OnCancel called");
			DoCleanup();
			_misty.ChangeLED(255, 0, 0, null);
		}

		/// <summary>
		/// Called when the skill times out
		/// </summary>
		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			_misty.SkillLogger.LogVerbose($"HelloLocomotionSkill : OnTimeout called");
			DoCleanup();
			_misty.ChangeLED(0, 0, 255, null);
		}

		/// <summary>
		/// OnPause simply calls OnCancel in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Pause is not implemented by default and that command is ignored
			_misty.SkillLogger.LogVerbose($"HelloLocomotionSkill : OnPause called");
		}

		/// <summary>
		/// OnResume simply calls OnStart in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Resume is not implemented by default and that command is ignored
			_misty.SkillLogger.LogVerbose($"HelloLocomotionSkill : OnResume called");
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
				_heartbeatTimer?.Dispose();
				_moveArmsTimer?.Dispose();
				_moveHeadTimer?.Dispose();
				_ledTimer?.Dispose();
			}

			// Free any unmanaged objects here.
			_disposed = true;
		}

		/// <summary>
		/// Skill Finalizer/Destructor
		/// </summary>
		~HelloLocomotionSkill()
		{
			Dispose(false);
		}
		
		#endregion

		
		#region User Created Helper Methods

		/// <summary>
		/// Do timeout or cancel cleanup
		/// </summary>
		private void DoCleanup()
		{
			_misty.Stop(null);
			_misty.StopKeyPhraseRecognition(null);
			_misty.StopFaceRecognition(null);
		}

		#endregion

		#region User Created Helper Methods

		/// <summary>
		/// Register the desired startup events, more may be registered separately as needed
		/// </summary>
		private void RegisterEvents()
		{
			//Voice Rec
			_misty.RegisterKeyPhraseRecognizedEvent(KeyPhraseRecognizedCallback, 0, true, null, null);

			//Face rec
			_misty.RegisterFaceRecognitionEvent(FaceRecCallback, 0, false, null, null, null);

			//Bumps
			_misty.RegisterBumpSensorEvent(BumpCallback, 0, true, null, null, null);
			
			//Cap touch
			_misty.RegisterCapTouchEvent(CapTouchCallback, 0, true, null, null, null);
			
			//TOF
			List<TimeOfFlightValidation> tofFrontRightValidations = new List<TimeOfFlightValidation>();
			tofFrontRightValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.DistanceInMeters, Comparison = ComparisonOperator.LessThanOrEqual, ComparisonValue = 0.15 });
			tofFrontRightValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.SensorName, Comparison = ComparisonOperator.Equal, ComparisonValue = TimeOfFlightPosition.FrontRight });
			_misty.RegisterTimeOfFlightEvent(TOFRangeCallback, 50, true, tofFrontRightValidations, null, null);

			List<TimeOfFlightValidation> tofFrontLeftValidations = new List<TimeOfFlightValidation>();
			tofFrontLeftValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.DistanceInMeters, Comparison = ComparisonOperator.LessThanOrEqual, ComparisonValue = 0.15 });
			tofFrontLeftValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.SensorName, Comparison = ComparisonOperator.Equal, ComparisonValue = TimeOfFlightPosition.FrontLeft });
			_misty.RegisterTimeOfFlightEvent(TOFRangeCallback, 50, true, tofFrontLeftValidations, null, null);

			List<TimeOfFlightValidation> tofBackValidations = new List<TimeOfFlightValidation>();
			tofBackValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.DistanceInMeters, Comparison = ComparisonOperator.LessThanOrEqual, ComparisonValue = 0.15 });
			tofBackValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.SensorName, Comparison = ComparisonOperator.Equal, ComparisonValue = TimeOfFlightPosition.Back });
			_misty.RegisterTimeOfFlightEvent(TOFRangeCallback, 50, true, tofBackValidations, null, null);

		}

		#endregion

		#region User Created Callbacks
		
		/// <summary>
		/// Called on the heartbeat timer tick to assess the current state of the robot and make a driving decision
		/// </summary>
		/// <param name="info"></param>
		public void HeartbeatCallback(object info)
		{
			_goingForward = !_goingForward;

			if (!_misty.Wait(0)) { return; }

			if (_randomGenerator.Next(1, 5) == 1)
			{
				var angular = _randomGenerator.Next(10, 15);
				if (_randomGenerator.Next(1, 2) == 1)
				{
					angular = -angular;
				}
				_misty.Drive(0, angular, null);
				_misty.SkillLogger.Log($"HelloLocomotionSkill : DRIVING: linear 0 angular {angular}");
			}
			else
			{
				var linear = _randomGenerator.Next(15, 25);

				var angular = _randomGenerator.Next(0, linear - 15 < 0 ? 0 : linear - 15);
				if (_randomGenerator.Next(1, 2) == 1)
				{
					angular = -angular;
				}

				if (!_goingForward)
				{
					linear = -linear;
				}

				_misty.Drive(linear, angular, null);
				_misty.SkillLogger.Log($"HelloLocomotionSkill : DRIVING: linear {linear} angular {angular}");
			}
		}

		/// <summary>
		/// Called on the timer tick to send a random move head command to the robot
		/// </summary>
		/// <param name="info"></param>
		public void MoveHeadCallback(object info)
		{
			_misty.MoveHead(_randomGenerator.Next(-30, 15), _randomGenerator.Next(-30, 30), _randomGenerator.Next(-70, 70), _randomGenerator.Next(10, 30), AngularUnit.Degrees, null);
		}

		/// <summary>
		/// Called on the timer tick to send a random move arm command to the robot
		/// </summary>
		/// <param name="info"></param>
		public void MoveArmCallback(object info)
		{
			_misty.MoveArms(_randomGenerator.Next(-90, 90), _randomGenerator.Next(-90, 90), _randomGenerator.Next(10, 40), _randomGenerator.Next(10, 40), null, AngularUnit.Degrees, null);
		}

		/// <summary>
		/// Called on the timer tick to send a random change LED command to the robot
		/// </summary>
		/// <param name="info"></param>
		public void ChangeLEDCallback(object info)
		{
			_misty.ChangeLED((uint)_randomGenerator.Next(0, 256), (uint)_randomGenerator.Next(0, 256), (uint)_randomGenerator.Next(0, 256), null);
		}

		/// <summary>
		/// Callback called when the bump sensor is contacted or released
		/// This method only process IsContacted events
		/// </summary>
		/// <param name="bumpEvent"></param>
		private void BumpCallback(IBumpSensorEvent bumpEvent)
		{			
			if(!bumpEvent.IsContacted)
			{
				return;
			}

			_misty.SkillLogger.Log($"HelloLocomotionSkill : BumpCallback: => {bumpEvent.IsContacted}");
			_misty.Stop(null);

			switch(bumpEvent.SensorPosition)
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

		/// <summary>
		/// Callback called when there is a new TimrOfFlight event
		/// </summary>
		/// <param name="tofEvent"></param>
		private void TOFRangeCallback(ITimeOfFlightEvent tofEvent)
		{
			_misty.SkillLogger.Log($"HelloLocomotionSkill : TOFRangeCallback {tofEvent.SensorPosition.ToString()} {tofEvent.DistanceInMeters} - goingforward = {_goingForward}");

			if (tofEvent.DistanceInMeters <= 0.1 && tofEvent.Status == 0)
			{
				if (_goingForward &&
					(tofEvent.SensorPosition == TimeOfFlightPosition.FrontLeft ||
					tofEvent.SensorPosition == TimeOfFlightPosition.FrontRight ||
					tofEvent.SensorPosition == TimeOfFlightPosition.FrontCenter))
				{
			
					_misty.SkillLogger.Log($"HelloLocomotionSkill : Driving forward attempt while something in front");
					_misty.Stop(null);

				}
				else if (!_goingForward && tofEvent.SensorPosition == TimeOfFlightPosition.Back)
				{
				
					_misty.SkillLogger.Log($"HelloLocomotionSkill : Driving backward attempt while something behind");
					_misty.Stop(null);
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
			if(capTouchEvent.IsContacted)
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
						_misty.DisplayImage("JoyGoofy.jpg", 1, null);
						break;
					case CapTouchPosition.Left:
						_misty.PlayAudio("e_Terror.jpg", 100, null);
						_misty.DisplayImage("s_Fear.wav", 1, null);
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

			if(!_misty.Wait(3000)) { return; }
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
			
			if(!_misty.Wait(5000)) { return; }
			_misty.DisplayImage("e_ContentLeft.jpg", 1, null);
			if (!_misty.Wait(5000)) { return; }
			_misty.RegisterFaceRecognitionEvent(FaceRecCallback, 0, false, null, null, null);
		}

		#endregion
	}
}