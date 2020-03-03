using System;
using System.Collections.Generic;
using System.Threading;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Events;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.Common.Data;

namespace SkillLibrary
{
	/// <summary>
	/// Simple fun skill that just drives based upon time of flight data
	/// Place your hand in front of the time of flights to make it drive in the opposite direction
	/// Closer to the tofs makes it move faster
	/// Also adds most of the previous skills' functionality to give the robot a fuller personality
	/// Assumes defaults assets are on the robot, update the names of assets if they have changed
	/// </summary>
	public class ForceDriving : IMistySkill
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
		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("ForceDriving", "e886e306-0ac1-49d8-9933-7d469546633a")
		{
			TimeoutInSeconds = 60 * 10 //runs for 10 minutes or until the skill is cancelled
		};

		/// <summary>
		///	This method is called by the wrapper to set your robot interface
		///	You need to save this off in the local variable commented on above as you are going use it to call the robot
		/// </summary>
		/// <param name="robotInterface"></param>
		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
			_misty.SkillLogger.LogLevel = SkillLogLevel.Verbose;
		
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
				
				RegisterEvents();

				//_heartbeatTimer = new Timer(HeartbeatCallback, null, 3000, 3000);
				_moveHeadTimer = new Timer(MoveHeadCallback, null, 10000, 10000);
				_moveArmsTimer = new Timer(MoveArmCallback, null, 10000, 10000);
				_ledTimer = new Timer(ChangeLEDCallback, null, 2000, 2000);
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log($"BackAndForthSkill : OnStart: => Exception", ex);
			}
		}

		/// <summary>
		/// Called when the skill is cancelled
		/// </summary>
		/// <param name="parameters"></param>
		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			_misty.SkillLogger.LogVerbose($"BackAndForthSkill : OnCancel called");
			DoCleanup();
			_misty.ChangeLED(255, 0, 0, null);
		}

		/// <summary>
		/// Called when the skill times out
		/// </summary>
		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			_misty.SkillLogger.LogVerbose($"BackAndForthSkill : OnTimeout called");
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
			_misty.SkillLogger.LogVerbose($"BackAndForthSkill : OnPause called");
		}

		/// <summary>
		/// OnResume simply calls OnStart in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Resume is not implemented by default and that command is ignored
			_misty.SkillLogger.LogVerbose($"BackAndForthSkill : OnResume called");
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
		~ForceDriving()
		{
			Dispose(false);
		}
		
		#endregion

		#region User Created Helper Methods

		private void DoCleanup()
		{
			_misty.Stop(null);
			_misty.StopKeyPhraseRecognition(null);
			_misty.StopFaceRecognition(null);
		}

		#endregion

		#region  User Created Callbacks

		private void MoveHeadCallback(object info)
		{
			_misty.MoveHead(_randomGenerator.Next(-30, 15), _randomGenerator.Next(-30, 30), _randomGenerator.Next(-70, 70), _randomGenerator.Next(10, 30), AngularUnit.Degrees, null);
		}

		private void MoveArmCallback(object info)
		{
			_misty.MoveArms(_randomGenerator.Next(-90, 90), _randomGenerator.Next(-90, 90), 100, 100, null, AngularUnit.Degrees, null);
		}

		private void ChangeLEDCallback(object info)
		{
			_misty.ChangeLED((uint)_randomGenerator.Next(0, 256), (uint)_randomGenerator.Next(0, 256), (uint)_randomGenerator.Next(0, 256), null);
		}

		/// <summary>
		/// Register the desired startup events, more may be registered separately as needed
		/// </summary>
		private void RegisterEvents()
		{
			//Bumps
			_misty.RegisterBumpSensorEvent(BumpCallback, 0, true, null, null, null);

			//Cap touch
			_misty.RegisterCapTouchEvent(CapTouchCallback, 0, true, null, null, null);

			//TOF
			List<TimeOfFlightValidation> tofFrontRightValidations = new List<TimeOfFlightValidation>();
			tofFrontRightValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.DistanceInMeters, Comparison = ComparisonOperator.LessThanOrEqual, ComparisonValue = 0.3 });
			tofFrontRightValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.SensorName, Comparison = ComparisonOperator.Equal, ComparisonValue = TimeOfFlightPosition.FrontRight });
			_misty.RegisterTimeOfFlightEvent(TOFRangeCallback, 300, true, tofFrontRightValidations, null, null);

			List<TimeOfFlightValidation> tofFrontLeftValidations = new List<TimeOfFlightValidation>();
			tofFrontLeftValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.DistanceInMeters, Comparison = ComparisonOperator.LessThanOrEqual, ComparisonValue = 0.3 });
			tofFrontLeftValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.SensorName, Comparison = ComparisonOperator.Equal, ComparisonValue = TimeOfFlightPosition.FrontLeft });
			_misty.RegisterTimeOfFlightEvent(TOFRangeCallback, 300, true, tofFrontLeftValidations, null, null);

			List<TimeOfFlightValidation> tofBackValidations = new List<TimeOfFlightValidation>();
			tofBackValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.DistanceInMeters, Comparison = ComparisonOperator.LessThanOrEqual, ComparisonValue = 0.3 });
			tofBackValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.SensorName, Comparison = ComparisonOperator.Equal, ComparisonValue = TimeOfFlightPosition.Back });
			_misty.RegisterTimeOfFlightEvent(TOFRangeCallback, 300, true, tofBackValidations, null, null);

			List<TimeOfFlightValidation> tofFrontCenterValidations = new List<TimeOfFlightValidation>();
			tofFrontCenterValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.DistanceInMeters, Comparison = ComparisonOperator.LessThanOrEqual, ComparisonValue = 0.3 });
			tofFrontCenterValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.SensorName, Comparison = ComparisonOperator.Equal, ComparisonValue = TimeOfFlightPosition.FrontCenter });
			_misty.RegisterTimeOfFlightEvent(TOFRangeCallback, 300, true, tofFrontCenterValidations, null, null);
		}

		#endregion

		#region User Created Callback Delegates

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

			_misty.SkillLogger.Log($"BackAndForthSkill : BumpCallback: => {bumpEvent.IsContacted}");
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
			_misty.SkillLogger.Log($"{(tofEvent.Status != 0 ? "WARNING!! " : "")}BackAndForthSkill : TOFRangeCallback {tofEvent.SensorPosition.ToString()} - Status:{tofEvent.Status} - Meters:{tofEvent.DistanceInMeters}");
			
			switch (tofEvent.SensorPosition)
			{
				case TimeOfFlightPosition.FrontLeft:
				case TimeOfFlightPosition.FrontRight:
				case TimeOfFlightPosition.FrontCenter:
					if (tofEvent.Status == 0)
					{
						if (tofEvent.DistanceInMeters <= 0.05)
						{
							_misty.DriveTime(-25, 0, 1000, null);
						}
						else if (tofEvent.DistanceInMeters <= 0.1)
						{
							_misty.DriveTime(-15, 0, 1000, null);
						}
						else if (tofEvent.DistanceInMeters <= 0.15)
						{
							_misty.DriveTime(-10, 0, 1000, null);
						}
						_misty.MoveArms(-60, -60, 100, 100, null, AngularUnit.Degrees, null);
					}
					break;
				case TimeOfFlightPosition.Back:
					if(tofEvent.Status == 0)
					{
						if (tofEvent.DistanceInMeters <= 0.05)
						{
							_misty.DriveTime(25, 0, 1000, null);
						}
						if (tofEvent.DistanceInMeters <= 0.1)
						{
							_misty.DriveTime(15, 0, 1000, null);
						}
						if (tofEvent.DistanceInMeters <= 0.15)
						{
							_misty.DriveTime(10, 0, 1000, null);
						}
						_misty.MoveArms(-30, -30, 100, 100, null, AngularUnit.Degrees, null);
					}
					break;
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
						_misty.DisplayImage("e_Love.jpg", 1 , null);
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