/******************************************************************************
*    Copyright 2020 Misty Robotics, Inc.
*    Licensed under the Apache License, Version 2.0 (the "License");
*    you may not use this file except in compliance with the License.
*    You may obtain a copy of the License at
*
*    http://www.apache.org/licenses/LICENSE-2.0
*
*    Unless required by applicable law or agreed to in writing, software
*    distributed under the License is distributed on an "AS IS" BASIS,
*    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
*    See the License for the specific language governing permissions and
*    limitations under the License.
******************************************************************************/

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Responses;
using MistyRobotics.Tools.Web;

namespace MistySkillTypes
{
	/// <summary>
	/// Collection of generic skill helper methods.
	/// </summary>
	public class SkillHelper : IDisposable
	{
		#region Private Members

		private readonly IRobotMessenger _misty;

		private double _leftEncoderValue;
		private ConcurrentQueue<double> _leftEncoderValues;
		private TofValues _tofValues = new TofValues();
		private bool _stopHazardState;
		private bool _stopHazardLatching;
		private SemaphoreSlim _headPositionSemaphore;
		private HeadPosition _headPosition;
		private bool _abort;

		#endregion

		#region Public Methods

		public SkillHelper(IRobotMessenger misty)
		{
			_misty = misty ?? throw new ArgumentNullException(nameof(misty));

			_misty.RegisterDriveEncoderEvent(EncoderEventCallback, 100, true, null, "SkillHelperEncoderEvent", OnResponse);
			_misty.RegisterIMUEvent(ImuEventCallback, 100, true, null, "SkillHelperIMUEvent", OnResponse);
			_misty.RegisterTimeOfFlightEvent(TofEventCallback, 0, true, null, "SkillHelperTofEventEvent", OnResponse);
			_misty.RegisterHazardNotificationEvent(HazardEventCallback, 0, true, "SkillHelperHazardEvent", OnResponse);
		}

		public void LogMessage(string msg)
		{
			// Write to the skill log file and the console.
			Debug.WriteLine(msg);
			_misty.SkillLogger.LogInfo(msg);
		}

		public void Abort()
		{
			_abort = true;
		}

		public double ImuYaw { get; private set; }

		public double ImuRoll { get; private set; }

		public double ImuPitch { get; private set; }

		/// <summary>
		/// Drive straight for the specified distance in meters at a medium speed.
		/// Confirm movement with encoder values and retry if needed.
		/// If stopped due to a hazard wait a little bit for it to go away and then continue.
		/// </summary>
		/// <param name="distance"></param>
		/// <param name="slow"></param>
		public async Task<bool> DriveAsync(double distance, bool slow = false)
		{
			if (Math.Abs(distance) < 0.001)
			{
				return true;
			}

			bool success = true;

			try
			{
				// Clear encoder values.
				int maxWait = 0;
				while (_leftEncoderValue != 0 && maxWait++ < 10)
				{
					_misty.DriveHeading(0, 0, 100, false, OnResponse);
					await Task.Delay(200);
				}
				if (_leftEncoderValue != 0)
				{
					LogMessage("Failed to reset encoder values.");
					return false;
				}

				_leftEncoderValues = new ConcurrentQueue<double>();
				_stopHazardLatching = false;
				bool driving = true;
				bool sendCommand = true;

				maxWait = 0;
				while (driving)
				{
					if (sendCommand)
					{
						// Arbitrary medium speed based upon distance.
						int duration = (int)(500 + Math.Abs(distance) * 2500);
						if (slow)
						{
							duration = 2 * duration;
						}

						LogMessage($"Sending drive command with a distance of {distance:f3} meters and a duration of {duration} ms.");
						while (!_leftEncoderValues.IsEmpty) // Clearing the encoder queue
						{
							_leftEncoderValues.TryDequeue(out double r);
						}
						_misty.DriveHeading(0, Math.Abs(distance), duration, distance < 0, OnResponse);
						sendCommand = false;
						await Task.Delay(1000);
					}

					if (_abort) return false;
					await Task.Delay(1000);

					// Check that we really moved and if we're done.
					// Drive command could have been dropped and/or a hazard could have stopped the robot.
					double[] encoderValues = _leftEncoderValues.ToArray();
					if (encoderValues.Length > 1) // encoder values should be arriving at 5Hz.
					{
						double distanceDriven = Math.Abs(encoderValues[encoderValues.Length - 1] / 1000.0);
						//LogMessage($"Distance driven: {distanceDriven}.");
						if (distanceDriven > Math.Abs(0.99 * distance) ||
						   Math.Abs(distanceDriven - Math.Abs(distance)) < 0.1)
						{
							LogMessage($"Completed drive with distance of {_leftEncoderValue / 1000.0:f3} meters.");
							driving = false;
						}
						else
						{
							// Not there yet. Everything progressing okay?
							if (_stopHazardLatching)
							{
								// We've stopped due to a hazard. Wait a bit for it to go away.
								LogMessage("Drive command paused for hazard.");
								_misty.PlayAudio("s_anger.wav", 100, OnResponse);
								int maxHazardWait = 0;
								while (_stopHazardState && maxHazardWait++ < 30)
								{
									if (_abort) return false;
									await Task.Delay(1000);
								}
								if (_stopHazardState)
								{
									// Still in hazard state. Give up.
									LogMessage("Giving up on drive command due to persistent hazard condition.");
									success = false;
									driving = false;
								}
								else
								{
									// We're out of hazard state.
									LogMessage("Out of hazard state.");
									distance -= encoderValues[encoderValues.Length - 1] / 1000.0;
									_stopHazardLatching = false;
									sendCommand = true;
								}
							}
							if (encoderValues.Length > 2 && Math.Abs(encoderValues[encoderValues.Length - 1] - encoderValues[encoderValues.Length - 2]) < 0.0001)
							{
								// Encoder value not changing. Need to send drive command again.
								LogMessage($"Encoder values not changing. Distance driven so far is {distanceDriven}.");
								distance -= encoderValues[encoderValues.Length - 1] / 1000.0;
								sendCommand = true;
							}
						}
					}
					else
					{
						// Something is wrong if we get here.
						// For now, just continue and hope for the best :).
						LogMessage("Not receiving encoder messages. Can't verify drive commands.");
					}
				}
			}
			catch(Exception ex)
			{
				LogMessage("Exception occurred within DriveAsync: " + ex.Message);
				success = false;
			}
			finally
			{
				_leftEncoderValues = null;
			}

			return success;
		}

		private void LogEncoderValues()
		{
			string s = "";

			if (_leftEncoderValues != null)
			{
				foreach (var v in _leftEncoderValues)
				{
					s += ", " + v;
				}
				s = s.Substring(2);
			}

			LogMessage($"Encoder values are: {s}.");
		}

		/// <summary>
		/// Turn N degrees at medium speed.
		/// Confirm with IMU values and retry if needed.
		/// </summary>
		/// <param name="degrees"></param>
		/// <returns></returns>
		public async Task<bool> TurnAsync(double degrees)
		{
			if (Math.Abs(degrees) < 2)
			{
				return true;
			}

			double initialYaw = ImuYaw;
			bool success = true;
			try
			{
				// Normalize degrees to be between -180 and 180.
				degrees = degrees % 360;
				if (degrees > 180)
					degrees = 360 - degrees;
				else if (degrees < -180)
					degrees = 360 + degrees;

				// Get medium speed duration.
				int duration = (int)(500 + Math.Abs(degrees) * 4000.0 / 90.0);

				// Send command
				LogMessage($"Sending turn command for {degrees:f2} degrees in {duration} ms.");
				_misty.DriveArc(ImuYaw + degrees, 0, duration, false, OnResponse);
				await Task.Delay(2000);

				// Turns less then about 3 degrees don't do anything.
				// So in that case don't bother checking or waiting any longer.
				if (Math.Abs(degrees) > 3)
				{
					// Make sure that the command worked and we are turning.
					int retries = 0;
					while (retries++ < 3 && Math.Abs(AngleDelta(ImuYaw, initialYaw)) < 1.0)
					{
						LogMessage($"Sending turn command for {degrees:f2} degrees in {duration} ms.");
						_misty.DriveArc(ImuYaw + degrees, 0, duration, false, OnResponse);
						await Task.Delay(1000);
						if (_abort) return false;
					}

					// Wait for turn to complete.
					retries = 0;
					double yawBefore = ImuYaw + 500;
					while (Math.Abs(AngleDelta(ImuYaw, yawBefore)) > 1.0 && retries++ < 20)
					{
						yawBefore = ImuYaw;
						await Task.Delay(500);
						if (_abort) return false;
					}
					await Task.Delay(250); // A little extra padding for last degree.
				}
			}
			catch (Exception ex)
			{
				success = false;
				LogMessage("Exception occurred within TurnAsync: " + ex.Message);
			}

			LogMessage($"Completed turn of {AngleDelta(ImuYaw, initialYaw):f2} degrees.");

			return success;
		}

		private double AngleDelta(double angle1, double angle2)
		{
			double a1 = angle1 % 360;
			a1 = a1 >= 0 ? a1 : 360 + a1;

			double a2 = angle2 % 360;
			a2 = a2 >= 0 ? a2 : 360 + a2;

			return a1 - a2;
		}

		public TofValues GetTofValues()
		{
			return _tofValues;
		}

		public async Task MoveHeadAsync(double pitchDegrees, double rollDegrees, double yawDegrees)
		{
			_misty.RegisterActuatorEvent(ActuatorEventCallback, 0, true, null, "SkillHelperActuatorEventCallback", OnResponse);

			// We move head to non-final position first so that we can verify change in position.
			double firstPitchDegrees = pitchDegrees + 10;
			if (pitchDegrees > 0)
			{
				firstPitchDegrees = pitchDegrees - 10;
			}
			_misty.MoveHead(firstPitchDegrees, rollDegrees, yawDegrees, 70, MistyRobotics.Common.Types.AngularUnit.Degrees, OnResponse);
			await Task.Delay(3000);

			_headPosition = new HeadPosition();
			_headPositionSemaphore = new SemaphoreSlim(0);
			await _headPositionSemaphore.WaitAsync(5000);
			double initPitch = _headPosition.Pitch.HasValue ? _headPosition.Pitch.Value : 100;
			LogMessage($"Head position after pre-move: {_headPosition.Pitch:f2}, {_headPosition.Roll:f2}, {_headPosition.Yaw:f2}.");

			// Now move head to final position.
			_headPosition = new HeadPosition();
			int retries = 0;
			while ((!_headPosition.Pitch.HasValue || (_headPosition.Pitch.HasValue && initPitch != 100 && Math.Abs(_headPosition.Pitch.Value - initPitch) < 2)) && retries++ < 3)
			{
				_misty.MoveHead(pitchDegrees, rollDegrees, yawDegrees, 70, MistyRobotics.Common.Types.AngularUnit.Degrees, OnResponse);
				await Task.Delay(5000);
				if (_abort) break;

				_headPositionSemaphore = new SemaphoreSlim(0);
				await _headPositionSemaphore.WaitAsync(5000);
				LogMessage($"Head position after move: {_headPosition.Pitch:f2}, {_headPosition.Roll:f2}, {_headPosition.Yaw:f2}.");
			}

			_misty.UnregisterEvent("SkillHelperActuatorEventCallback", OnResponse);
		}

		public async Task DisableHazardSystemAsync()
		{
			string endpoint = "http://10.10.10.10/api/hazard/updatebasesettings";

			string data = "{\"timeOfFlightThresholds\":[" +
				"{\"sensorName\":\"TOF_DownFrontRight\",\"threshold\":0}," +
				"{\"sensorName\":\"TOF_DownFrontLeft\",\"threshold\":0}," +
				"{\"sensorName\":\"TOF_DownBackRight\",\"threshold\":0}," +
				"{\"sensorName\":\"TOF_DownBackLeft\",\"threshold\":0}," +
				"{\"sensorName\":\"TOF_Right\",\"threshold\":0}," +
				"{\"sensorName\":\"TOF_Left\",\"threshold\":0}," +
				"{\"sensorName\":\"TOF_Center\",\"threshold\":0}," +
				"{\"sensorName\":\"TOF_Back\",\"threshold\":0}]," +
				"\"bumpSensorsEnabled\":[" +
				"{\"sensorName\":\"Bump_FrontRight\",\"enabled\":false}," +
				"{\"sensorName\":\"Bump_FrontLeft\",\"enabled\":false}," +
				"{\"sensorName\":\"Bump_RearRight\",\"enabled\":false}," +
				"{\"sensorName\":\"Bump_RearLeft\",\"enabled\":false}]}";

			WebMessenger wm = new WebMessenger();
			var r = await wm.PostRequest(endpoint, data, "application/json");
			LogMessage($"Post {endpoint}. Result is {r.HttpCode} - {r.Response}.");
		}

		public async Task EnableHazardSystemAsync()
		{
			string endpoint = "http://10.10.10.10/api/hazard/updatebasesettings";

			string data = "{\"timeOfFlightThresholds\":[" +
				"{\"sensorName\":\"TOF_DownFrontRight\",\"threshold\":0.06}," +
				"{\"sensorName\":\"TOF_DownFrontLeft\",\"threshold\":0.06}," +
				"{\"sensorName\":\"TOF_DownBackRight\",\"threshold\":0.06}," +
				"{\"sensorName\":\"TOF_DownBackLeft\",\"threshold\":0.06}," +
				"{\"sensorName\":\"TOF_Right\",\"threshold\":0.215}," +
				"{\"sensorName\":\"TOF_Left\",\"threshold\":0.215}," +
				"{\"sensorName\":\"TOF_Center\",\"threshold\":0.215}," +
				"{\"sensorName\":\"TOF_Back\",\"threshold\":0.215}]," +
				"\"bumpSensorsEnabled\":[" +
				"{\"sensorName\":\"Bump_FrontRight\",\"enabled\":true}," +
				"{\"sensorName\":\"Bump_FrontLeft\",\"enabled\":true}," +
				"{\"sensorName\":\"Bump_RearRight\",\"enabled\":true}," +
				"{\"sensorName\":\"Bump_RearLeft\",\"enabled\":true}]}";

			WebMessenger wm = new WebMessenger();
			var r = await wm.PostRequest(endpoint, data, "application/json");
			LogMessage($"Post {endpoint}. Result is {r.HttpCode} - {r.Response}.");
		}

		#endregion

		#region Private Methods

		private void TofEventCallback(ITimeOfFlightEvent eventResponse)
		{
			switch (eventResponse.SensorPosition)
			{
				case MistyRobotics.Common.Types.TimeOfFlightPosition.FrontLeft:
					if (eventResponse.Status == 0)
						_tofValues.FrontLeft = eventResponse.DistanceInMeters;
					else
						_tofValues.FrontLeft = 5;
					break;
				case MistyRobotics.Common.Types.TimeOfFlightPosition.FrontCenter:
					if (eventResponse.Status == 0)
						_tofValues.FrontCenter = eventResponse.DistanceInMeters;
					else
						_tofValues.FrontCenter = 5;
					break;
				case MistyRobotics.Common.Types.TimeOfFlightPosition.FrontRight:
					if (eventResponse.Status == 0)
						_tofValues.FrontRight = eventResponse.DistanceInMeters;
					else
						_tofValues.FrontRight = 5;
					break;
				case MistyRobotics.Common.Types.TimeOfFlightPosition.Back:
					if (eventResponse.Status == 0)
						_tofValues.Back = eventResponse.DistanceInMeters;
					else
						_tofValues.Back = 5;
					break;
			}
		}

		private void HazardEventCallback(IHazardNotificationEvent eventResponse)
		{
			_stopHazardState = eventResponse.StopHazard;
			
			if (_stopHazardState && !_stopHazardLatching)
			{
				_stopHazardLatching = true;
			}
		}

		private void ActuatorEventCallback(IActuatorEvent eventResponse)
		{
			if (_headPosition == null)
				_headPosition = new HeadPosition();

			switch (eventResponse.SensorPosition)
			{
				case MistyRobotics.Common.Types.ActuatorPosition.HeadPitch:
					_headPosition.Pitch = eventResponse.ActuatorValue;
					break;
				case MistyRobotics.Common.Types.ActuatorPosition.HeadYaw:
					_headPosition.Yaw = eventResponse.ActuatorValue;
					break;
				case MistyRobotics.Common.Types.ActuatorPosition.HeadRoll:
					_headPosition.Roll = eventResponse.ActuatorValue;
					break;
			}

			if (_headPosition.Pitch.HasValue && _headPosition.Yaw.HasValue && _headPosition.Roll.HasValue)
			{
				_headPositionSemaphore?.Release();
			}
		}

		private void EncoderEventCallback(IDriveEncoderEvent eventResponse)
		{
			_leftEncoderValue = eventResponse.LeftDistance;

			if (_leftEncoderValues != null)
			{
				_leftEncoderValues.Enqueue(_leftEncoderValue);
			}
		}

		private void ImuEventCallback(IIMUEvent eventResponse)
		{
			// Angles come between 0 and 360.
			// Store angles between -180 and 180
			// Also, we sometimes see large negative spikes of noise. Ignore these. I believe that this is a robot specific problem.

			if (eventResponse.Yaw >= 0)
			{
				if (eventResponse.Yaw > 180)
					ImuYaw = eventResponse.Yaw - 360;
				else
					ImuYaw = eventResponse.Yaw;
			}

			if (eventResponse.Pitch >= 0)
			{
				if (eventResponse.Pitch > 180)
					ImuPitch = eventResponse.Pitch - 360;
				else
					ImuPitch = eventResponse.Pitch;
			}

			if (eventResponse.Roll >= 0)
			{
				if (eventResponse.Roll > 180)
					ImuRoll = eventResponse.Roll - 360;
				else
					ImuRoll = eventResponse.Roll;
			}
		}

		private void OnResponse(IRobotCommandResponse commandResponse)
		{

		}

		#endregion

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects).
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.
				LogMessage("SkillHelper.Dispose");
				_misty?.UnregisterEvent("SkillHelperIMUEvent", OnResponse);
				_misty?.UnregisterEvent("SkillHelperEncoderEvent", OnResponse);
				_misty.UnregisterEvent("SkillHelperHazardEvent", OnResponse);

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~SkillHelper() {
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

	public class TofValues
	{
		public double? FrontLeft { get; set; }
		public double? FrontCenter { get; set; }
		public double? FrontRight { get; set; }
		public double? Back { get; set; }
	}

	public class HeadPosition
	{
		public double? Pitch { get; set; }
		public double? Roll { get; set; }
		public double? Yaw { get; set; }
	}
}
