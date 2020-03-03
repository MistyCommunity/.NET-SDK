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
using System.Collections.Generic;
using System.Threading.Tasks;
using MistyRobotics.Common.Data;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Responses;

namespace MistySkillTypes
{
	public class ChargerDock2
	{
		#region Description

		// This class encapsulates one possible autonomous charger docking algorithm.
		// Assumes that Misty is near enough to the charger to detect it.
		// Intended to work with charger with alignment wedge on it.
		// 1. Spins in a circle to find the charger.
		// 2. Moves so that it is lined up with charger.
		// 3. Backs on to charger.

		#endregion

		#region Coordinate systems

		// Charger pose comes as a column major matrix with upper left 3x3 being the rotation matrix and fourth column containing X, Y, Z.
		// Origin of the charger pose is Structure Core right camera. Pose represents the center of the charger platform.
		// Uses Occipital units: Z forward, X right, Y down.
		//
		// IMU yaw is in Misty units: + CCW.
		// Absolute IMU yaw value is arbitrary. Goes to 0 when the firmware starts. We use desired change in yaw to turn.

		#endregion

		#region Private Members

		private readonly IRobotMessenger _misty;
		private readonly SkillHelper _skillHelper;
		private readonly double _headPitchOffset = 0;
		private readonly double _headRollOffset = -5;
		private readonly double _headYawOffset = -2;

		private ChargerPosition _chargerPose;
		private Queue<ChargerPosition> _previousChargerPose;
		private bool _abort;
		private double _initialPitch;
		private double _initialYaw;
		private SlamStatusDetails _slamStatus;
		private bool _poseLock;
		private bool _charging;

		private const float CENTER_OFFSET = 0.04f;					// Camera for dock position is to the right of Misty's center
		private const double ALIGNED_X = 0.03;						// Tolerance for considering us aligned well enough to dock
		private const double ALIGNED_EULER_YAW = 3;					// Tolerance for considering us aligned well enough to dock
		private const double MISTY_ON_WEDGE_ROLL = 5;               // Degrees of roll where we assume Misty has driven up onto charger guide
		private const double MISTY_ON_WEDGE_PITCH = 5;              // Degrees of pitch where we assume Misty has driven up onto charger guide
		private const double DOCKED_YAW_TOLERANCE = 3;              // How close we expected the final, docked Misty yaw to be compared to the expected value
		private const double IDEAL_ALIGNMENT_DISTANCE = 1.0;		// How far from the charger should we be (Z) for best alignment?
		private const double INITIAL_ROTATION_BEFORE_SWEEP = 35;    // When Misty first starts looking for charger, turn this far and then sweep in other direction
		private const double CHARGER_DOCK_OVERSHOOT = 0.2;			// How much further we backup on to charger beyond a perfect mount
		private const double SWEEP_STEP_DEGREES = 10;				// Sweep for charger step size
		private const int DOCKING_MAX_RETRIES = 3;                  // How many times to try complete docking process
		private const int ALIGN_MAX_RETRIES = 10;                    // How many times to try and align with charger per overall docking attempt

		private const int DockingStationDetectorEnabled = 0x10000;

		#endregion

		#region Public Methods

		public ChargerDock2(IRobotMessenger misty, SkillHelper skillHelper, 
							double headPitchOffset, double headRollOffset, double headYawOffset)
		{
			_misty = misty ?? throw new ArgumentNullException(nameof(misty));
			_skillHelper = skillHelper ?? throw new ArgumentNullException(nameof(skillHelper));
			_headPitchOffset = headPitchOffset;
			_headRollOffset = headRollOffset;
			_headYawOffset = headYawOffset;
		}

		public async Task<bool> DockAsync()
		{
			_skillHelper.LogMessage("Execute charger docking.");

			_misty.RegisterBatteryChargeEvent(BatteryChargingMessage, 250, true, null, "BatteryChargingEvent", OnResponse);

			_previousChargerPose = new Queue<ChargerPosition>();
			_abort = false;

			// Pitch sometimes has an offset even when Misty is level.
			_initialPitch = AxisLimited(_skillHelper.ImuPitch);
			_skillHelper.LogMessage($"Initial pitch is {_initialPitch:f3}.");

			_initialYaw = _skillHelper.ImuYaw;

			_misty.RegisterChargerPoseEvent(ChargerPoseMessage, 100, true, "ChargerPoseEvent", OnResponse);
			if (!await StartDockDetectorAsync())
			{
				Cleanup();
				return false;
			}

			// Try to dock N times.
			bool docked = false;
			int retries = 0;
			while (!docked && retries++ < DOCKING_MAX_RETRIES)
			{
				docked = await ExecuteDockAsync();
			}

			Cleanup();

			return docked;
		}

		public void Abort()
		{
			_abort = true;
			_skillHelper.LogMessage("Charger docking aborted.");

			_skillHelper.Abort();
			Cleanup();
		}

		#endregion

		#region Private Methods

		private async Task<bool> ExecuteDockAsync()
		{
			_charging = false;

			// Set head position.
			await _skillHelper.MoveHeadAsync(_headPitchOffset, _headRollOffset, _headYawOffset);
			if (_abort) return false;

			// Find the charger.
			if (!await FindChargerAsync())
				return false;

			// Get Misty 'perfectly' aligned with charger.
			int retries = 0;
			while (Math.Abs(_chargerPose.X) > ALIGNED_X || Math.Abs(_chargerPose.EulerYaw) > ALIGNED_EULER_YAW)
			{
				if (retries++ > ALIGN_MAX_RETRIES)
				{
					_skillHelper.LogMessage("Failed to align with charger.");
					return false;
				}

				_skillHelper.LogMessage($"Charger position is [{_chargerPose.X:f3}, {_chargerPose.Y:f3}, {_chargerPose.Z:f3}] meters. Euler yaw is {_chargerPose.EulerYaw:f3} degrees.");
				await FaceChargerAsync(ALIGNED_X);
				if (_abort) return false;

				if (Math.Abs(_chargerPose.X) > ALIGNED_X || Math.Abs(_chargerPose.EulerYaw) > ALIGNED_EULER_YAW)
				{
					await AlignWithChargerAsync();
					if (Math.Abs(_chargerPose.Z - IDEAL_ALIGNMENT_DISTANCE) > .1)
						await _skillHelper.DriveAsync(_chargerPose.Z - IDEAL_ALIGNMENT_DISTANCE);
				}
			}

			_skillHelper.LogMessage($"Charger position is [{_chargerPose.X:f3}, {_chargerPose.Y:f3}, {_chargerPose.Z:f3}] meters. Euler yaw is {_chargerPose.EulerYaw:f3} degrees.");

			// Turn around.
			_skillHelper.LogMessage("Turning 180 degrees to face away from charger.");
			double chargerDistance = _chargerPose.Z;
			await _skillHelper.TurnAsync(-180);
			if (_abort) return false;

			// Back on to charger.
			_skillHelper.LogMessage($"Driving {chargerDistance + CHARGER_DOCK_OVERSHOOT:f3} meters to back on to charger.");
			await _skillHelper.DriveAsync(-chargerDistance - CHARGER_DOCK_OVERSHOOT, true);
			if (_abort) return false;

			// Check if we've ended up on top of the wedge.
			await Task.Delay(1000);
			_skillHelper.LogMessage($"Roll = {_skillHelper.ImuRoll:f3}. Pitch = {_skillHelper.ImuPitch:f3}.");
			if (Math.Abs(_skillHelper.ImuRoll) > MISTY_ON_WEDGE_ROLL || Math.Abs(AxisRotation(_initialPitch, _skillHelper.ImuPitch)) > MISTY_ON_WEDGE_PITCH)
			{
				// We're on the wedge. Drive away from the charger and try again.
				_skillHelper.LogMessage($"We appear to have driven on top of the alignment wedge. IMU roll is {_skillHelper.ImuRoll:f3}. IMU pitch is {_skillHelper.ImuPitch:f3}.");
				await _skillHelper.DriveAsync(IDEAL_ALIGNMENT_DISTANCE - 0.1);
				if (_abort) return false;
				await _skillHelper.TurnAsync(180);
				_chargerPose = null;
				return false;
			}

			// Check that we're fully docked: back up a little bit to get more aligned.
			_misty.DriveHeading(0, 0.2, 500, true, OnResponse);

			// It can take several seconds for the charging indicator to update...
			await Task.Delay(7000);

			// Check that we're charging.
			if (!_charging)
			{
				_misty.PlayAudio("s_Anger3.wav", 100, OnResponse);
				await _skillHelper.DriveAsync(IDEAL_ALIGNMENT_DISTANCE - 0.1);
				if (_abort) return false;
				await _skillHelper.TurnAsync(180);
				_chargerPose = null;
				return false;
			}

			_misty.PlayAudio("s_Ecstacy.wav", 100, OnResponse);

			return true;
		}

		private async Task<bool> FaceChargerAsync(double xTolerance)
		{
			try
			{
				// Turn to face directly at charger.
				_skillHelper.LogMessage("Turning to face directly at charger.");
				while (Math.Abs(_chargerPose.X) > xTolerance)
				{
					// If charger is to left of robot there will be a negative X which corresponds to a 
					// positive IMU based turn (CCW). So we invert bearing value here.
					var chargerBearing = -180.0 * Math.Atan(_chargerPose.X / _chargerPose.Z) / Math.PI;

					// Trying to make very small turns doesn't work. So we need to make larger step sizes.
					// We may need to overshoot and then come back and that's okay.
					if (Math.Abs(chargerBearing) < 3)
					{
						if (chargerBearing < 0)
							chargerBearing = -3;
						else
							chargerBearing = 3;
					}

					await _skillHelper.TurnAsync(chargerBearing);
					if (_abort) return false;
					await Task.Delay(1500);
					_skillHelper.LogMessage($"Charger position is [{_chargerPose.X:f3}, {_chargerPose.Y:f3}, {_chargerPose.Z:f3}] meters. Euler yaw is {_chargerPose.EulerYaw:f3} degrees. Turning {chargerBearing:f3} degrees.");
				}

				_skillHelper.LogMessage($"Charger position is [{_chargerPose.X:f3}, {_chargerPose.Y:f3}, {_chargerPose.Z:f3}] meters. Euler yaw is {_chargerPose.EulerYaw:f3} degrees.");
			}
			catch (Exception ex)
			{
				_skillHelper.LogMessage("An exception occurred within ChargerDock.FaceChargerAsync: " + ex.Message);
			}

			return true;
		}

		private async Task<bool> FindChargerAsync()
		{
			try
			{
				_skillHelper.LogMessage("Initiating search for charger.");

				// Clear pose and pause to see if we can already see the charger.
				_chargerPose = null;
				await Task.Delay(3000);
				if (_abort) return false;
				if (_chargerPose != null)
				{
					_skillHelper.LogMessage($"Charger at [{_chargerPose.X:f3}, {_chargerPose.Y:f3}, {_chargerPose.Z:f3}] meters. Euler yaw is {_chargerPose.EulerYaw:f3} degrees.");
				}
				else
				{
					// Perform a sweep to look for the charger.
					// This is based upon the assumption that we just drove back to the charger and are roughly facing it.
					await _skillHelper.TurnAsync(INITIAL_ROTATION_BEFORE_SWEEP);
					_chargerPose = null;
					if (_abort) return false;

					if (!await SweepForCharger())
					{
						// Did not find charger after a full spin. Check our TOF distance assuming that charger is against a wall.
						await _skillHelper.TurnAsync(AxisRotation(_skillHelper.ImuYaw, _initialYaw));
						if (_abort) return false;

						TofValues tofValues = _skillHelper.GetTofValues();
						if (_abort) return false;
						double? distance = tofValues.FrontCenter.HasValue ? tofValues.FrontCenter.Value : tofValues.FrontLeft.HasValue ? tofValues.FrontLeft.Value : tofValues.FrontRight.Value;
						if (distance.HasValue)
						{
							// Drive to about 1 meter from wall
							await _skillHelper.DriveAsync(distance.Value - 1.0);

							// Sweep again.
							await _skillHelper.TurnAsync(INITIAL_ROTATION_BEFORE_SWEEP);
							if (_abort) return false;
							if (!await SweepForCharger())
							{
								_skillHelper.LogMessage("Never found the charger.");
								return false;
							}
						}
					}
				}
				_skillHelper.LogMessage($"Charger at [{_chargerPose.X:f3}, {_chargerPose.Y:f3}, {_chargerPose.Z:f3}] meters. Euler yaw is {_chargerPose.EulerYaw:f3} degrees.");
			}
			catch(Exception ex)
			{
				_skillHelper.LogMessage("An exception occurred within ChargerDock.FindChargerAsync: " + ex.Message);
			}

			return true;
		}

		private async Task<bool> SweepForCharger()
		{
			double sweepStartImuYaw = _skillHelper.ImuYaw;
			int steps = 0;
			while (_chargerPose == null)
			{
				if (steps++ * SWEEP_STEP_DEGREES >= 360 && _skillHelper.ImuYaw < sweepStartImuYaw)
				{
					_skillHelper.LogMessage("Did not find charger.");
					return false;
				}

				await _skillHelper.TurnAsync(-SWEEP_STEP_DEGREES);
				if (_abort) return false;
			}

			return true;
		}

		private async Task<bool> AlignWithChargerAsync()
		{
			// Get charger offset.
			double chargerOffset = _chargerPose.Z * Math.Sin(Math.PI * _chargerPose.EulerYaw / 180.0);

			// Turn perpendicular to charger.
			if (chargerOffset > 0)
			{
				// Charger is rotated CW so turn to the right.
				_skillHelper.LogMessage($"Turning {90 - _chargerPose.EulerYaw:f3} degrees to be perpendicular to charger.");
				await _skillHelper.TurnAsync(90 - _chargerPose.EulerYaw);
			}
			else
			{
				// Charger is rotated CCW.
				_skillHelper.LogMessage($"Turning {-(90 + _chargerPose.EulerYaw):f3} degrees to be perpendicular to charger.");
				await _skillHelper.TurnAsync(-(90 + _chargerPose.EulerYaw));
			}
			if (_abort) return false;

			// Drive to be aligned with charger.
			_skillHelper.LogMessage($"Driving {Math.Abs(chargerOffset):f3} meters to be directly in front of charger.");
			await _skillHelper.DriveAsync(Math.Abs(chargerOffset));
			if (_abort) return false;

			// Turn towards charger.
			_skillHelper.LogMessage($"Turning 90 degrees to face charger.");
			if (chargerOffset > 0)
				await _skillHelper.TurnAsync(-90);
			else
				await _skillHelper.TurnAsync(90);
			await Task.Delay(1500);

			return true;
		}

		private double AxisRotation(double from, double to)
		{
			return AxisLimited((to - from) % 360);
		}

		/// <summary>
		/// Return equivalent angle between -180 and 180
		/// </summary>
		/// <param name="angle"></param>
		private double AxisLimited(double angle)
		{
			return angle > 180 ? angle - 360 : (angle < -180 ? angle + 360 : angle);
		}

		private void ChargerPoseMessage(IChargerPoseEvent eventResponse)
		{
			// Using averaged value of charger pose.
			if (_poseLock) return;

			_poseLock = true;

			try
			{
				if (_previousChargerPose.Count > 5)
					_previousChargerPose.Dequeue();

				var latestPose = new ChargerPosition()
				{
					X = eventResponse.Pose.HomogeneousMatrix[12],
					Y = eventResponse.Pose.HomogeneousMatrix[13],
					Z = eventResponse.Pose.HomogeneousMatrix[14],
					EulerYaw = (float)(180.0 * Math.Atan2(eventResponse.Pose.HomogeneousMatrix[6], eventResponse.Pose.HomogeneousMatrix[2]) / Math.PI)
				};
				_previousChargerPose.Enqueue(latestPose);

				var meanPose = new ChargerPosition();
				foreach (var pose in _previousChargerPose)
				{
					meanPose.X += pose.X;
					meanPose.Y += pose.Y;
					meanPose.Z += pose.Z;
					meanPose.EulerYaw += pose.EulerYaw;
				}

				meanPose.X /= _previousChargerPose.Count;
				meanPose.Y /= _previousChargerPose.Count;
				meanPose.Z /= _previousChargerPose.Count;
				meanPose.EulerYaw /= _previousChargerPose.Count;

				// Apply offset so that we can treat charger position as relative to the center
				// of the Occipital sensor.
				meanPose.X += CENTER_OFFSET;

				//_skillHelper.LogMessage($"Charger pose: X={meanPose.X} Z={meanPose.Z} EY={meanPose.EulerYaw}.");

				_chargerPose = meanPose;
			}
			finally
			{
				_poseLock = false;
			}
		}

		private void BatteryChargingMessage(IBatteryChargeEvent eventResponse)
		{
			_charging = eventResponse.IsCharging;
		}

		private void Cleanup()
		{
			_skillHelper.LogMessage("Charger docker cleanup.");

			_misty.StopLocatingDockingStation(5000, 5000, OnResponse);
			_misty.UnregisterEvent("ChargerPoseEvent", OnResponse);
			_misty.UnregisterEvent("BatteryChargingEvent", OnResponse);
		}

		private async Task<bool> StartDockDetectorAsync()
		{
			_misty.RegisterSlamStatusEvent(SlamStatusCallback, 0, true, "ChargerDockSlamStatus", null, OnResponse);

			_skillHelper.LogMessage("Start dock detector.");
			_misty.StartLocatingDockingStation(5000, 5000, OnResponse);
			await Task.Delay(3000);

			int retries = 0;
			while (retries++ < 3 && (_slamStatus == null || (_slamStatus.Status & DockingStationDetectorEnabled) == 0))
			{
				_skillHelper.LogMessage("Dock detector did not start. Restarting SLAM service.");
				_misty.RebootSlamSensor(OnResponse);
				await Task.Delay(5000);

				_skillHelper.LogMessage("Start dock detector.");
				_misty.StartLocatingDockingStation(5000, 5000, OnResponse);
				await Task.Delay(3000);
			}

			_misty.UnregisterEvent("ChargerDockSlamStatus", OnResponse);

			if (_slamStatus == null || (_slamStatus.Status & DockingStationDetectorEnabled) == 0)
				return false;
			else
				return true;
		}

		private void SlamStatusCallback(ISlamStatusEvent eventResponse)
		{
			_slamStatus = eventResponse.SlamStatus;
		}

		private void OnResponse(IRobotCommandResponse commandResponse)
		{

		}

		#endregion
	}
}
