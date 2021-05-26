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

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MistyRobotics.Common.Data;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Responses;
using Windows.Storage;

namespace MistyNavigation
{
	public class ChargerDockSmall
	{
		#region Description

		// This class encapsulates an autonomous charger docking algorithm.
		// Assumes that Misty is already facing the charger and near enough to detect it.
		// Intended to work with charger with alignment rail on it.

		#endregion

		#region Coordinate systems


		// Misty coordinates are: 
		//   X is straight ahead in meters.
		//   Y is to the left in meters.
		//   Yaw is positive rotation counter-clockwise in radians.

		#endregion

		#region Private Members

		private readonly IRobotMessenger _misty;
		private readonly SkillHelper _skillHelper;

		private bool _abort;
		private SlamStatusDetails _slamStatus;
		private bool _charging;
		private bool _chargeDetectorRunning;
		private Simple2DPose _chargerPose;

		// Individual robot corrections to get head straight and align with charger
		private float _charger_y_offset = 0;
		private float _head_yaw_offset = 0;
		private float _head_roll_offset = 0;

		private const double IDEAL_ALIGNMENT_DISTANCE = 0.3;		// How far from the charger should we be (Z) for best alignment?
		private const int DOCKING_MAX_RETRIES = 3;                  // How many times to try complete docking process
		private const int ALIGN_MAX_RETRIES = 10;					// How many times to try and align with charger per overall docking attempt

		private const int DockingStationDetectorEnabled = 0x10000;

		#endregion

		#region Public Methods

		public ChargerDockSmall(IRobotMessenger misty, SkillHelper skillHelper)
		{
			_misty = misty ?? throw new ArgumentNullException(nameof(misty));
			_skillHelper = skillHelper ?? throw new ArgumentNullException(nameof(skillHelper));

			Cleanup(); // just in case things weren't cleaned up during the previous run

			_ = LoadChargerOffsetsAsync();
		}

		public async Task<bool> DockAsync()
		{
			_abort = false;
			_skillHelper.MistySpeak("Initiating charger docking.");
			_skillHelper.LogMessage("Execute charger docking.");

			_misty.RegisterBatteryChargeEvent(BatteryChargingMessage, 250, true, null, "BatteryChargingEvent", OnResponse);
			_misty.RegisterChargerPoseEvent(ChargerPoseMessage, 100, true, "ChargerPoseEvent", OnResponse);

			if (!await StartChargerDetectionAsync())
			{
				Cleanup();
				return false;
			}

			// Try to dock N times.
			bool docked = false;
			int retries = 0;
			while (!docked && retries++ < DOCKING_MAX_RETRIES && !_abort)
			{
				docked = await ExecuteDockAsync(retries == DOCKING_MAX_RETRIES);
			}

			Cleanup();

			return docked;
		}

		public void Abort()
		{
			_skillHelper.LogMessage("Charger docking aborted.");
			_abort = true;
			Cleanup();
		}

		#endregion

		#region Private Methods

		private async Task LoadChargerOffsetsAsync()
		{
			try
			{
				if (File.Exists(@"c:\Data\Misty\SDK\SkillData\DockingOffsets.txt"))
				{
					StorageFolder sdkDataFolder = await StorageFolder.GetFolderFromPathAsync(@"c:\Data\Misty\SDK\SkillData");
					StorageFile file = await sdkDataFolder.GetFileAsync("DockingOffsets.txt");

					string text = await FileIO.ReadTextAsync(file);
					string[] lines = text.Split('\n');

					foreach (string line in lines)
					{
						string[] parts = line.Split('=');
						string parameter = parts[0].Trim().ToLower();
						switch (parameter)
						{
							case "charger y offset":
								_charger_y_offset = float.Parse(parts[1].Trim());
								break;
							case "head yaw offset":
								_head_yaw_offset = float.Parse(parts[1].Trim());
								break;
							case "head roll offset":
								_head_roll_offset = float.Parse(parts[1].Trim());
								break;
						}
					}
				}
				else
				{
					_skillHelper.LogMessage("No docking offsets file present.");
				}
			}
			catch (Exception ex)
			{
				_skillHelper.LogMessage("Failed to read docking offsets file: " + ex.Message);
			}

			_skillHelper.LogMessage($"Charger y offset = {_charger_y_offset}.");
			_skillHelper.LogMessage($"Head yaw offset = {_head_yaw_offset}.");
			_skillHelper.LogMessage($"Head roll offset = {_head_roll_offset}.");
		}

		private async Task<bool> ExecuteDockAsync(bool lastTry)
		{
			// Find the charger. It should be right in front of us.
			// Depending upon how far away from the charger we are we may need a different head pitch.
			// Start by assuming that we're fairly close to the charger which means we need to look more downward to see the charger.
			double headPitch = 35;
			await _skillHelper.MoveHeadAsync(headPitch, 0, 0);
			if (!await FindChargerAsync())
			{
				headPitch = 15;
				await _skillHelper.MoveHeadAsync(headPitch, 0, 0);
				if (!await FindChargerAsync())
				{
					_skillHelper.MistySpeak("I can't find the charger.");
					await Task.Delay(3000);
					return false;
				}
			}
			_skillHelper.MistySpeak("I see the charger.");

			_skillHelper.LogMessage($"Charger is located at [x, y, yaw] : [{_chargerPose.X:f3}, {_chargerPose.Y:f3}, {_chargerPose.Yaw.Degrees():f1}].");

			int retries = 0;
			while (_chargerPose.X - IDEAL_ALIGNMENT_DISTANCE > 0.04 || IDEAL_ALIGNMENT_DISTANCE - _chargerPose.X > 0.02 || 
				   Math.Abs(_chargerPose.Y) > 0.01 || Math.Abs(_chargerPose.Yaw.Degrees()) > 2.5)
			{
				var goalPose = new Simple2DPose()
				{
					X = IDEAL_ALIGNMENT_DISTANCE,
					Y = 0,
					Yaw = 0
				};

				MoveSequence moveSequence = NavigationHelper.CalculateMoveSequence(_chargerPose, goalPose);

				_skillHelper.LogMessage($"Movement sequence to goal is turn {moveSequence.Turn1.Degrees():f0} degrees, drive {moveSequence.DriveDistance:f3} meters, " +
					$"and turn {moveSequence.Turn2.Degrees():f0} degrees.");

				if (!await _skillHelper.TurnAsync(moveSequence.Turn1.Degrees()))
					return false;
				if (_abort) return false;

				if (!await _skillHelper.DriveAsync(moveSequence.DriveDistance, true))
					return false;
				if (_abort) return false;

				if (!await _skillHelper.TurnAsync(moveSequence.Turn2.Degrees()))
					return false;
				if (_abort) return false;

				if (headPitch != 35)
				{
					// In case we had to lift the head to originally find the charger.
					headPitch = 35;
					await _skillHelper.MoveHeadAsync(headPitch, 0, 0);
				}

				await Task.Delay(1000);
				if (!await CanSeeChargerAsync(1))
				{
					_skillHelper.LogMessage("Can no longer see the charger.");
					_skillHelper.MistySpeak("Uh oh. I can't see the charger any more.");
					return false;
				}

				_skillHelper.LogMessage($"Charger now located at [x, y, yaw] : [{_chargerPose.X:f3}, {_chargerPose.Y:f3}, {_chargerPose.Yaw.Degrees():f1}]. " +
					$"Error : [{(_chargerPose.X - goalPose.X):f3}, {(_chargerPose.Y - goalPose.Y):f3}, {(_chargerPose.Yaw.Degrees() - goalPose.Yaw.Degrees()):f1}].");

				if (retries++ > 5)
				{
					_skillHelper.LogMessage("Failed to line up with the charger.");
					_skillHelper.MistySpeak("I can't seem to line up right.");
					await Task.Delay(5000);
					return false;
				}
			}

			_skillHelper.MistySpeak("I should be all lined up now. Going to turn around and back on to the charger.");

			// Turn around.
			// Note that we do 2 x 91 degrees. Misty tends to turn slightly less than requested and any turn where the absolute
			// value is over 180 degrees will get converted to a <180 turn in the other direction.
			_skillHelper.LogMessage("Turning 180 degrees to face away from charger.");
			if (!await _skillHelper.TurnAsync(91))
				return false;
			if (_abort) return false;
			if (!await _skillHelper.TurnAsync(91))
				return false;
			if (_abort) return false;

			// Back on to charger.
			_skillHelper.LogMessage($"Driving {IDEAL_ALIGNMENT_DISTANCE:f3} meters to back on to charger.");
			if (!await _skillHelper.DriveAsync(-IDEAL_ALIGNMENT_DISTANCE - 0.1, true))
				return false;
			if (_abort) return false;
			
			// Move forward slightly.
			_misty.DriveHeading(0, 0.01, 1000, false, OnResponse);

			// It can take several seconds for the charging indicator to update...
			_charging = false;
			DateTime start = DateTime.Now;
			while (!_charging && DateTime.Now.Subtract(start).TotalSeconds < 8)
			{
				await Task.Delay(250);
			}

			// If charging then the battery current is a positive number about 0.4 Amps
			if (_charging)
			{
				_skillHelper.MistySpeak("Ahh. I feel the power.");
				await Task.Delay(2000);
			}
			else
			{
				if (!lastTry)
				{
					_skillHelper.MistySpeak("Hmm. I don't seem to be charging. I'm going to drive forward and try docking again.");
					_skillHelper.LogMessage("Did not detect that we're charging. Driving forward and trying again.");

					await _skillHelper.DriveAsync(IDEAL_ALIGNMENT_DISTANCE + 0.1);
					if (_abort) return false;
					await _skillHelper.TurnAsync(180);

					return false;
				}
			}

			return true;
		}

		private async Task<bool> FindChargerAsync()
		{
			_skillHelper.LogMessage("Initiating search for charger.");

			bool chargerFound = await CanSeeChargerAsync(2);
			if (!chargerFound)
			{
				// Perform a sweep to look for the charger.
				//_skillHelper.MistySpeak("I don't see the charger so I'm going to start rotating to look for it.");
				if (!await _skillHelper.TurnAsync(45))
					return false;
				if (_abort) return false;

				int maxTurns = 10;
				while (!chargerFound && --maxTurns > 0)
				{
					chargerFound = await CanSeeChargerAsync(1);
					if (_abort) return false;
					if (!chargerFound)
					{
						if (!await _skillHelper.TurnAsync(-12))
							return false;
					}
				}
			}

			if (!chargerFound)
			{
				// If we never saw the charger then return to our orginal yaw.
				if (!await _skillHelper.TurnAsync(45))
					return false;
			}

			return chargerFound;
		}

		/// <summary>
		/// Check to see if we can see the charger. Wait up to 'seconds' seconds.
		/// </summary>
		private async Task<bool> CanSeeChargerAsync(int seconds)
		{
			_chargerPose.X = 0;

			DateTime start = DateTime.Now;
			while (_chargerPose.X == 0 && DateTime.Now.Subtract(start).TotalSeconds < seconds)
			{
				await Task.Delay(250);
			}

			return _chargerPose.X != 0;
		}

		private void ChargerPoseMessage(IChargerPoseEvent eventResponse)
		{
			float[] matrix = eventResponse.Pose.HomogeneousMatrix;
			//_skillHelper.LogMessage($"Charger pose: [{matrix[0]}, {matrix[1]}, {matrix[2]}, {matrix[3]}, {matrix[4]}, {matrix[5]}, {matrix[6]}, {matrix[7]}, {matrix[8]}, {matrix[9]}, {matrix[10]}, {matrix[11]}, {matrix[12]}, {matrix[13]}, {matrix[14]}, {matrix[15]}]");

			// Occipital coordinates have left-right axis as positive to the right.
			// Occipital origin is 0.035mm to the right of Misty.
			// So we add 0.035 to the received value to get a left-right value for Misty's body.
			// The _charger_y_offset is an optionally manually set offset that may help some Misty's dock more reliably.
			matrix[12] += (0.035f + _charger_y_offset);

			_chargerPose = NavigationHelper.ConvertPose(matrix);
			
			//_skillHelper.LogMessage("Charger pose: " + _chargerPose.ToString());
		}

		private void BatteryChargingMessage(IBatteryChargeEvent eventResponse)
		{
			_charging = eventResponse.IsCharging;
		}

		private void Cleanup()
		{
			_skillHelper.LogMessage("Charger dock cleanup.");

			_misty.StopLocatingDockingStation(5000, 5000, OnResponse);
			_misty.UnregisterEvent("ChargerPoseEvent", OnResponse);
			_misty.UnregisterEvent("BatteryChargingEvent", OnResponse);
		}

		/// <summary>
		/// Encapsulates starting charger detection with retries.
		/// </summary>
		private async Task<bool> StartChargerDetectionAsync()
		{
			_chargeDetectorRunning = false;
			_misty.RegisterSlamStatusEvent(SlamStatusCallback, 0, true, "ChargerDockSlamStatus", null, OnResponse);

			_skillHelper.LogMessage("Starting charger detector.");
			_misty.StartLocatingDockingStation(5000, 5000, true, OnResponse);
			await Task.Delay(5000);

			int retries = 0;
			while (retries++ < 3 && !_chargeDetectorRunning && !_abort)
			{
				if (_slamStatus == null)
				{
					_skillHelper.LogMessage($"Charger detection did not start. Occipital sensor status is null. Restarting Occipital service.");
				}
				else
				{
					string s = string.Empty;
					foreach (var status in _slamStatus.StatusList)
						s += status + "|";
					_skillHelper.LogMessage($"Charger detection did not start. Occipital sensor status is {_slamStatus.SensorStatus}. Occipital status list is {s}. Restarting Occipital service.");
				}

				_misty.DisableSlamService(OnResponse);
				await Task.Delay(5000);
				_misty.EnableSlamService(OnResponse);
				if (_abort) break;

				await Task.Delay(8000);
				if (_abort) break;

				_misty.StartLocatingDockingStation(5000, 5000, true, OnResponse);
				await Task.Delay(5000);
			}

			_misty.UnregisterEvent("ChargerDockSlamStatus", OnResponse);

			//_misty.SetSlamIrExposureAndGain(0.01, 2.0, OnResponse);

			if (!_chargeDetectorRunning)
			{
				_skillHelper.LogMessage("Failed to start the charger detector.");
				_skillHelper.MistySpeak("I could not start the charger detector.");
				return false;
			}
			else
			{
				return true;
			}
		}

		private void SlamStatusCallback(ISlamStatusEvent eventResponse)
		{
			_slamStatus = eventResponse.SlamStatus;

			//string s = string.Empty;
			//foreach (var status in _slamStatus.StatusList)
			//	s += status + "|";
			//_skillHelper.LogMessage($"Occipital sensor status is {_slamStatus.SensorStatus}. Occipital status list is {s}.");

			if (!_chargeDetectorRunning && _slamStatus.StatusList.Contains("DockingStationDetectorEnabled") && 
				_slamStatus.StatusList.Contains("DockingStationDetectorProcessing") && _slamStatus.StatusList.Contains("Streaming"))
			{
				_chargeDetectorRunning = true;
			}
		}

		private void OnResponse(IRobotCommandResponse commandResponse)
		{

		}

		#endregion
	}
}
