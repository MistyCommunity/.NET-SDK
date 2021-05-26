/**********************************************************************
	Copyright 2021 Misty Robotics
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
#define DEBUG_MESSAGES

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Responses;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MistyNavigation
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
		private DateTime _lastEncoderValue = DateTime.Now;
		private bool _stopHazardState;
		private bool _stopHazardLatching;
		private HeadPosition _headPosition;
		private bool _abort;

		#endregion

		#region Public Methods

		public SkillHelper(IRobotMessenger misty)
		{
			_misty = misty ?? throw new ArgumentNullException(nameof(misty));

			LogMessage("SkillHelper initiating. Registering event callbacks for encoders and IMU.");

			_misty.RegisterDriveEncoderEvent(EncoderEventCallback, 100, true, null, "SkillHelperEncoderEvent", OnResponse);
			_misty.RegisterIMUEvent(ImuEventCallback, 100, true, null, "SkillHelperIMUEvent", OnResponse);

			// THE HAZARD HANDLING CODE IS CURRENTLY DISABLED
			//_misty.RegisterHazardNotificationEvent(HazardEventCallback, 0, true, "SkillHelperHazardEvent", OnResponse);
		}

		public void LogMessage(string msg)
		{
			// Write to the skill log file and the console.
			Debug.WriteLine(DateTime.Now + " " + msg);
			_misty.SkillLogger.LogInfo(msg);
		}

		public void Abort()
		{
			_abort = true;
			Cleanup();
		}

		public double ImuYaw { get; private set; }

		public double ImuRoll { get; private set; }

		public double ImuPitch { get; private set; }

		public DateTime LastEncoderMessageReceived { get; private set; } = DateTime.MinValue;

		public DateTime LastImuMessageReceived { get; private set; } = DateTime.MinValue;

		/// <summary>
		/// Drive straight for the specified distance in meters at a medium speed.
		/// Confirm movement with encoder values and retry if needed.
		/// If stopped due to a hazard wait a little bit for it to go away and then continue.
		/// </summary>
		/// <param name="distance"></param>
		/// <param name="slow"></param>
		public async Task<bool> DriveAsync(double distance, bool slow = false)
		{
			if (Math.Abs(distance) < 0.003)
			{
				// Can't drive this short a distance
				return true;
			}

			if (DateTime.Now.Subtract(LastEncoderMessageReceived).TotalSeconds > 1)
			{
				LogMessage($"Cannot carry out a drive command because encoder messages are not being received. Last encoder message received at {LastEncoderMessageReceived}.");
				MistySpeak("Encoder messages are not being received. Path following aborted.");
				return false;
			}

			bool success = true;
			try
			{
				// Clear encoder values by sending a drive command with a distance of 0.
				int encoderResets = 0;
				while (_leftEncoderValue != 0 && encoderResets++ < 10)
				{
					_misty.DriveHeading(0, 0, 100, false, OnResponse);
					await Task.Delay(500);
				}
				if (_leftEncoderValue != 0)
				{
					LogMessage($"Failed to reset encoder values. Last encoder value received at {LastEncoderMessageReceived} with a value of {_leftEncoderValue}.");
					return false;
				}

				_leftEncoderValues = new ConcurrentQueue<double>();
				_stopHazardLatching = false;
				bool driving = true;
				bool sendCommand = true;
				int duration = 0;
				int retries = 0;
				DateTime start = DateTime.Now;

				while (driving)
				{
					if (sendCommand)
					{
						// Arbitrary medium speed based upon distance.
						duration = (int)(500 + Math.Abs(distance) * 3000);
						if (slow)
						{
							duration = 3 * duration;
						}

						while (!_leftEncoderValues.IsEmpty) // Clearing the encoder queue
						{
							_leftEncoderValues.TryDequeue(out double r);
						}
						LogMessage($"Sending drive command with a distance of {distance:f3} meters and a duration of {duration} ms.");
						_misty.DriveHeading(0, Math.Abs(distance), duration, distance < 0, OnResponse);
						start = DateTime.Now;
						sendCommand = false;
						await Task.Delay(1000);
					}

					if (_abort) return false;
					await Task.Delay(1500);

					// Check that we really moved and if we're done.
					// Drive command could have been dropped and/or a hazard could have stopped the robot.
					double[] encoderValues = _leftEncoderValues.ToArray();
					if (encoderValues.Length > 1) // encoder values should be arriving at 5Hz.
					{
						double distanceDriven = Math.Abs(encoderValues[encoderValues.Length - 1] / 1000.0);
						if (distanceDriven > Math.Abs(0.99 * distance) || Math.Abs(distanceDriven - Math.Abs(distance)) < 0.1)
						{
							LogMessage($"Completed drive with distance of {_leftEncoderValue / 1000.0:f3} meters.");
							driving = false;
						}
						else
						{
							// THE FOLLOWING CODE AROUND HAZARDS IS CURRENTLY INACTIVE AS THE HAZARD SYSTEM IS DISABLED BY THE SKILL
							// Not there yet. Everything progressing okay?
							if (_stopHazardLatching)
							{
								// We've stopped due to a hazard. Wait a bit for it to go away.
								LogMessage("Drive command paused for hazard.");
								_misty.PlayAudio("s_Anger.wav", 100, OnResponse);
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
							if (encoderValues[encoderValues.Length - 1] == 0)
							{
								// Encoder values are not changing. Try sending drive command again.
								if (retries++ > 5)
								{
									// Don't try forever.
									LogMessage("Unable to complete drive command successfully.");
									success = false;
									driving = false;
								}
								else
								{
									sendCommand = true;
								}
							}
							// Don't wait forever.
							if (DateTime.Now.Subtract(start).TotalMilliseconds > (5 + 2 * duration))
							{
								LogMessage($"Time out waiting for completion of drive. Completed distance of {_leftEncoderValue / 1000.0:f3} meters.");
								success = false;
								driving = false;
							}
						}
					}
					else
					{
						LogMessage($"Not receiving encoder messages. Last encoder value recieved at {_lastEncoderValue}.");
						success = false;
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

		/// <summary>
		/// Turn N degrees at medium speed.
		/// Confirm with IMU values and retry if needed.
		/// </summary>
		/// <param name="degrees"></param>
		/// <returns></returns>
		public async Task<bool> TurnAsync(double degrees)
		{
			if (DateTime.Now.Subtract(LastImuMessageReceived).TotalSeconds > 1)
			{
				LogMessage($"Cannot carry out a turn command because IMU messages are not being received. Last IMU message received at {LastImuMessageReceived}.");
				MistySpeak("IMU messages are not being received. Path following aborted.");
				return false;
			}

			// Normalize degrees to be between -180 and 180.
			degrees = degrees % 360;
			if (degrees > 180)
				degrees = degrees - 360;
			else if (degrees < -180)
				degrees = 360 + degrees;

			if (Math.Abs(degrees) < 2)
			{
				// Can't turn that little.
				return true;
			}

			double initialYaw = ImuYaw;
			bool success = true;
			try
			{
				// Get medium speed duration.
				int duration = (int)(500 + Math.Abs(degrees) * 4000.0 / 90.0);

				// Send command
				LogMessage($"Sending turn command for {degrees:f1} degrees in {duration} ms.");
				_misty.DriveArc(ImuYaw + degrees, 0, duration, false, OnResponse);
				await Task.Delay(2000);
				if (_abort) return false;

				// Turns less then about 3 degrees don't do anything.
				// So in that case don't bother checking or waiting any longer.
				if (Math.Abs(degrees) > 3)
				{
					// Make sure that the command worked and we are turning.
					int retries = 0;
					while (retries++ < 3 && Math.Abs(AngleDelta(ImuYaw, initialYaw)) < 1.0)
					{
						LogMessage($"Sending turn command for {degrees:f1} degrees in {duration} ms.");
						_misty.DriveArc(ImuYaw + degrees, 0, duration, false, OnResponse);
						await Task.Delay(1000);
						if (_abort) return false;
					}

					// Wait for turn to complete.
					retries = 0;
					double yawBefore = ImuYaw + 180;
					while (Math.Abs(AngleDelta(yawBefore, ImuYaw)) > 1.0 && retries++ < 25)
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

			double turned = AngleDelta(initialYaw, ImuYaw);
			if (Math.Abs(AngleDelta(turned, degrees)) > 5)
			{
				success = false;
				LogMessage($"Only turned {turned:f1} degrees. Expected {degrees:f1} degrees.");
				
			}
			else
			{
				success = true;
				LogMessage($"Completed turn of {turned:f1} degrees.");
			}

			return success;
		}

		/// <summary>
		/// Move head with retries if pitch isn't where we want it to be.
		/// </summary>
		public async Task MoveHeadAsync(double pitchDegrees, double rollDegrees, double yawDegrees)
		{
			_misty.RegisterActuatorEvent(ActuatorEventCallback, 0, true, null, "SkillHelperActuatorEventCallback", OnResponse);

			LogMessage($"Move head: {pitchDegrees:f0}, {rollDegrees:f0}, {yawDegrees:f0}.");

			_headPosition = new HeadPosition();
			int retries = 0;
			while ((!_headPosition.Pitch.HasValue || Math.Abs(_headPosition.Pitch.Value - pitchDegrees) > 3) && retries++ < 3)
			{
				_misty.MoveHead(pitchDegrees, rollDegrees, yawDegrees, 70, MistyRobotics.Common.Types.AngularUnit.Degrees, OnResponse);
				await Task.Delay(3000); // totally arbitrary wait
				if (_abort) break;
			}

			LogMessage($"Head position after move: {_headPosition.Pitch:f0}, {_headPosition.Roll:f0}, {_headPosition.Yaw:f0}.");

			_misty.UnregisterEvent("SkillHelperActuatorEventCallback", OnResponse);
		}

		public void DisableHazardSystem()
		{
			_misty.UpdateHazardSettings(new MistyRobotics.Common.Data.HazardSettings()
			{
				DisableBumpSensors = true,
				DisableTimeOfFlights = true
			}, OnResponse);
		}

		public void EnableHazardSystem()
		{
			_misty.UpdateHazardSettings(new MistyRobotics.Common.Data.HazardSettings()
			{
				RevertToDefault = true
			}, OnResponse);
		}

		public async Task TakePictureAsync(string fileName)
		{
			try
			{
				ITakePictureResponse response = await _misty.TakePictureAsync("sadf", false, false, true, 640, 480);
				if (response.Status == MistyRobotics.Common.Types.ResponseStatus.Success)
				{
					StorageFolder sdkFolder = await StorageFolder.GetFolderFromPathAsync(@"c:\Data\Misty\SDK");
					StorageFolder folder = null;
					if (await sdkFolder.TryGetItemAsync("Images") == null)
					{
						folder = await sdkFolder.CreateFolderAsync("Images");
					}
					else
					{
						folder = await sdkFolder.GetFolderAsync("Images");
					}

					IBuffer buff = WindowsRuntimeBufferExtensions.AsBuffer(response.Data.Image.ToArray());
					InMemoryRandomAccessStream ms = new InMemoryRandomAccessStream();
					await ms.WriteAsync(buff);
					BitmapDecoder decoder = await BitmapDecoder.CreateAsync(ms);
					SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
					StorageFile file = await folder.CreateFileAsync(fileName);
					using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
					{
						BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
						encoder.SetSoftwareBitmap(softwareBitmap);
						await encoder.FlushAsync();
					}
				}
			}
			catch (Exception ex)
			{
				LogMessage("Failed to save picture: " + ex.Message);
			}
		}

		public void MistySpeak(string message)
		{
#if DEBUG_MESSAGES
			try
			{
				_misty.Speak(message, true, "na", OnResponse);
			}
			catch (Exception) { }
#endif
		}

#endregion

#region Private Methods

		private void Cleanup()
		{
			LogMessage("SkillHelper cleanup");
			_misty?.UnregisterEvent("SkillHelperIMUEvent", OnResponse);
			_misty?.UnregisterEvent("SkillHelperEncoderEvent", OnResponse);
			//_misty.UnregisterEvent("SkillHelperHazardEvent", OnResponse);
		}

		/// <summary>
		/// Compute the difference in degrees of going from angle1 to angle2.
		/// </summary>
		private double AngleDelta(double angle1, double angle2)
		{
			double a1 = angle1 % 360;
			a1 = a1 >= 0 ? a1 : 360 + a1;

			double a2 = angle2 % 360;
			a2 = a2 >= 0 ? a2 : 360 + a2;

			double diff = a2 - a1;
			diff = diff > 180 ? diff - 360 : diff;
			diff = diff < -180 ? 360 + diff : diff;

			return diff;
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
		}

		private void EncoderEventCallback(IDriveEncoderEvent eventResponse)
		{
			if (LastEncoderMessageReceived == DateTime.MinValue)
			{
				LastEncoderMessageReceived = DateTime.Now;
				LogMessage("First encoder event message received.");
			}

			LastEncoderMessageReceived = DateTime.Now;

			_leftEncoderValue = eventResponse.LeftDistance;
			_lastEncoderValue = DateTime.Now;

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

			if (LastImuMessageReceived == DateTime.MinValue)
			{
				LastImuMessageReceived = DateTime.Now;
				LogMessage("First IMU event message received.");
			}

			LastImuMessageReceived = DateTime.Now;

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
			//Debug.WriteLine(commandResponse.Status);
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
