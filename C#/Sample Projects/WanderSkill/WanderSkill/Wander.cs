using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MistyRobotics.Common.Data;
using MistyRobotics.SDK.Events;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;
using WanderSkill.DriveManagers;
using WanderSkill.Types;

namespace WanderSkill
{
	internal class Wander : IMistySkill
	{
		private IRobotMessenger _misty;

		private CurrentObstacleState _currentObstacleState;
		
		private DriveHeartbeat _driveHeartbeat;
		
		private Random _randomGenerator = new Random();

		private BaseDrive _driveManager;

		private bool _debugMode = true;
		
		private DriveMode _driveMode { get; set; } = DriveMode.Wander;

		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("WanderSkill", "d8d01527-f1c2-4f2a-8843-36cb710ecfa7")
		{
			AllowedCleanupTimeInMs = 2000,
			TimeoutInSeconds = int.MaxValue
		};
		
		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
			_currentObstacleState = new CurrentObstacleState();
			_misty.RegisterForSDKLogEvents(PrintMessage);
		}
		
		public void OnStart(object sender, IDictionary<string, object> parameters)
		{
			_misty.TransitionLED(255, 140, 0, 0, 0, 255, LEDTransition.Breathe, 1000, null);
			_misty.Wait(3000);
			_misty.ChangeLED(0, 0, 255, null);

			ProcessParameters(parameters);
			RegisterEvents();
			
			//Start listening for heartbeat ticks
			_driveHeartbeat.HeartbeatTick += HeartbeatCallback;
		}

		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			OnCancel(sender, parameters);
		}

		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			OnStart(sender, parameters);
		}

		public async void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			_driveHeartbeat.HeartbeatTick -= HeartbeatCallback;
			await _misty.StopAsync();
			_misty.TransitionLED(0, 0, 255, 255, 0, 0, LEDTransition.TransitOnce, 2000, null);
		}

		public async void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			_driveHeartbeat.HeartbeatTick -= HeartbeatCallback;
			await _misty.StopAsync();
			_misty.TransitionLED(0, 0, 255, 255, 140, 0, LEDTransition.TransitOnce, 2000, null);
		}

		private void PrintMessage(object sender, LogMessage message)
		{
			Debug.WriteLine($"SDK Message: {message.Message}");
		}
		
		public void HeartbeatCallback(object sender, DateTime _lastHeartbeatTime)
		{
			_misty.SkillLogger.LogInfo($"FrontRightTOF = {_currentObstacleState.FrontRightTOF} - FrontLeftTOF = {_currentObstacleState.FrontLeftTOF} - Paused = {_driveHeartbeat.HeartbeatPaused}");
			if(!_misty.Wait(0)) { return; }

			switch (_driveMode)
			{	
				case DriveMode.Careful:
					_driveManager.Drive();
					break;
				case DriveMode.Wander:
					if (_driveHeartbeat.HeartbeatPaused)
					{
						return;
					}
					//Wander2 does a little more complex driving so turn off the hearbeat until this drive action is complete
					_driveHeartbeat.PauseHeartbeat();
					_driveManager.Drive();
					_driveHeartbeat.ContinueHeartbeat();
					break;
			}		
		}

		private void ProcessParameters(IDictionary<string, object> parameters)
		{
			try
			{
				object debugMode = parameters.FirstOrDefault(x => x.Key.ToLower().Trim() == "debugMode").Value;
				if (debugMode != null)
				{
					_debugMode = Convert.ToBoolean(debugMode);
				}

				if(_debugMode)
				{
					_misty.SkillLogger.LogLevel = SkillLogLevel.Verbose;
				}

				object driveMode = parameters.FirstOrDefault(x => x.Key.ToLower().Trim() == "drivemode").Value;
				if (driveMode != null && 
					Enum.TryParse(typeof(DriveMode), Convert.ToString(driveMode).Trim(), true, out object driveModeEnum))
				{
					_driveMode = (DriveMode)driveModeEnum;
				}

				switch (_driveMode)
				{
					case DriveMode.Wander:
						_driveManager = new WanderDrive(_misty, _currentObstacleState, _debugMode);
						_driveHeartbeat = new DriveHeartbeat(150);
						break;
					case DriveMode.Careful:
						_driveManager = new CarefulDrive(_misty, _currentObstacleState, _debugMode);
						_driveHeartbeat = new DriveHeartbeat(3000);
						break;
				}
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log("Failed handling startup parameters", ex);
			}
		}
		
		
		private void RegisterEvents()
		{
			//Register Bump Sensors with a callback
			_misty.RegisterBumpSensorEvent(BumpCallback, 50, true, null, null, null);

			//Front Right Time of Flight
			List<TimeOfFlightValidation> tofFrontRightValidations = new List<TimeOfFlightValidation>();
			tofFrontRightValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.SensorName, Comparison = ComparisonOperator.Equal, ComparisonValue = TimeOfFlightPosition.FrontRight });
			_misty.RegisterTimeOfFlightEvent(TOFFRRangeCallback, 0, true, tofFrontRightValidations, "FrontRight", null);
			
			//Front Left Time of Flight
			List<TimeOfFlightValidation> tofFrontLeftValidations = new List<TimeOfFlightValidation>();
			tofFrontLeftValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.SensorName, Comparison = ComparisonOperator.Equal, ComparisonValue = TimeOfFlightPosition.FrontLeft });
			_misty.RegisterTimeOfFlightEvent(TOFFLRangeCallback, 0, true, tofFrontLeftValidations, "FrontLeft", null);

			//Front Center Time of Flight
			List<TimeOfFlightValidation> tofFrontCenterValidations = new List<TimeOfFlightValidation>();
			tofFrontCenterValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.SensorName, Comparison = ComparisonOperator.Equal, ComparisonValue = TimeOfFlightPosition.FrontCenter });
			_misty.RegisterTimeOfFlightEvent(TOFCRangeCallback, 0, true, tofFrontCenterValidations, "FrontCenter", null);
			
			//Back Time of Flight
			List<TimeOfFlightValidation> tofBackValidations = new List<TimeOfFlightValidation>();
			tofBackValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.SensorName, Comparison = ComparisonOperator.Equal, ComparisonValue = TimeOfFlightPosition.Back });
			_misty.RegisterTimeOfFlightEvent(TOFBRangeCallback, 0, true, tofBackValidations, "Back", null);
			
			//Setting debounce a little higher to avoid too much traffic
			//Firmware will do the actual stop for edge detection
			List<TimeOfFlightValidation> tofFrontRightEdgeValidations = new List<TimeOfFlightValidation>();
			tofFrontRightEdgeValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.SensorName, Comparison = ComparisonOperator.Equal, ComparisonValue = TimeOfFlightPosition.DownwardFrontRight });
			_misty.RegisterTimeOfFlightEvent(FrontEdgeCallback, 1000, true, tofFrontRightEdgeValidations, "FREdge", null);

			List<TimeOfFlightValidation> tofFrontLeftEdgeValidations = new List<TimeOfFlightValidation>();
			tofFrontLeftEdgeValidations.Add(new TimeOfFlightValidation { Name = TimeOfFlightFilter.SensorName, Comparison = ComparisonOperator.Equal, ComparisonValue = TimeOfFlightPosition.DownwardFrontLeft });
			_misty.RegisterTimeOfFlightEvent(FrontEdgeCallback, 1000, true, tofFrontLeftEdgeValidations, "FLEdge", null);
		}

		private bool TryGetAdjustedDistance(ITimeOfFlightEvent tofEvent, out double distance)
		{
			distance = 0;
			//   0 = valid range data
			// 101 = sigma fail - lower confidence but most likely good
			// 104 = Out of bounds - Distance returned is greater than distance we are confident about, but most likely good
			if (tofEvent.Status == 0 || tofEvent.Status == 101 || tofEvent.Status == 104)
			{
				distance = tofEvent.DistanceInMeters;
			}
			else if (tofEvent.Status == 102)
			{
				//102 generally indicates nothing substantial is in front of the robot so the TOF is returning the floor as a close distance
				//So ignore the disance returned and just set to 2 meters
				distance = 2;
			}
			else
			{
				//TOF returning uncertain data or really low confidence in distance, ignore value 
				return false;
			}
			return true;
		}

		public void TOFFLRangeCallback(ITimeOfFlightEvent tofEvent)
		{
			if(TryGetAdjustedDistance(tofEvent, out double distance))
			{
				_currentObstacleState.FrontLeftTOF = distance;
			}	
		}

		public void TOFFRRangeCallback(ITimeOfFlightEvent tofEvent)
		{
			if (TryGetAdjustedDistance(tofEvent, out double distance))
			{
				_currentObstacleState.FrontRightTOF = distance;
			}
		}

		public void TOFCRangeCallback(ITimeOfFlightEvent tofEvent)
		{
			if (TryGetAdjustedDistance(tofEvent, out double distance))
			{
				_currentObstacleState.FrontCenterTOF = distance;
			}
		}

		public void TOFBRangeCallback(ITimeOfFlightEvent tofEvent)
		{
			if (TryGetAdjustedDistance(tofEvent, out double distance))
			{
				_currentObstacleState.BackTOF = distance;
			}
		}

		public void BumpCallback(IBumpSensorEvent bumpEvent)
		{
			switch (bumpEvent.SensorPosition)
			{
				case BumpSensorPosition.FrontRight:
					if (bumpEvent.IsContacted)
					{
						_currentObstacleState.FrontRightBumpContacted = true;
					}
					else
					{
						_currentObstacleState.FrontRightBumpContacted = false;
					}
					break;
				case BumpSensorPosition.FrontLeft:
					if (bumpEvent.IsContacted)
					{
						_currentObstacleState.FrontLeftBumpContacted = true;
					}
					else
					{
						_currentObstacleState.FrontLeftBumpContacted = false;
					}
					break;
				case BumpSensorPosition.BackRight:
					if (bumpEvent.IsContacted)
					{
						_currentObstacleState.BackRightBumpContacted = true;
					}
					else
					{
						_currentObstacleState.BackRightBumpContacted = false;
					}
					break;
				case BumpSensorPosition.BackLeft:
					if (bumpEvent.IsContacted)
					{
						_currentObstacleState.BackLeftBumpContacted = true;
					}
					else
					{
						_currentObstacleState.BackLeftBumpContacted = false;
					}
					break;
			}
		}

		private void FrontEdgeCallback(ITimeOfFlightEvent edgeEvent)
		{
			switch (edgeEvent.SensorPosition)
			{
				case TimeOfFlightPosition.DownwardFrontRight:
					_currentObstacleState.FrontRightEdgeTOF = edgeEvent.DistanceInMeters;
					break;
				case TimeOfFlightPosition.DownwardFrontLeft:
					_currentObstacleState.FrontLeftEdgeTOF = edgeEvent.DistanceInMeters;
					break;
			}
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

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~Wander()
		{
			Dispose(false);
		}

		#endregion
	}
}
