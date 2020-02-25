//////////////////////////////////////////////////////////////////////////////////////////
//
// This is sample prototype quality Misty skill code that represents one way to achieve certain
// functionality. Use as you wish.
//
//////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MistyRobotics.Common.Data;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Responses;

namespace MistySkillTypes
{
	public class ChargerDock
	{
		#region Description

		// This class encapsulates one possible autonomous charger docking algorithm.
		// It assumes that the robot has just driven back to the charger and is about one meter away facing the charger.
		// It attempts to point directly at the charger to determine the charger location, drives directly in front of the charger,
		// turns around, backs up on to the charger, and then 'shimmies' until charging is happening and the IMU yaw is close
		// to the starting value.
		// There are many magic numbers that could be further optimized.

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
		private readonly double _expectedFinalImuYaw;

		private ChargerPosition _chargerPose;
		private Queue<ChargerPosition> _previousChargerPose;
		private bool _charging;
		private bool _abort;
		private double _initialPitch;
		private SlamStatusDetails _slamStatus;
		private bool _poseLock;

		private const double CENTER_OFFSET = 0.02;					// Camera for dock position is to the right of Misty's center
		private const double ALIGNED_X = 0.05;						// Tolerance for considering us aligned well enough to dock
		private const double ALIGNED_EULER_YAW = 3;					// Tolerance for considering us aligned well enough to dock
		private const double MISTY_ON_WEDGE_ROLL = 5;               // Degrees of roll where we assume Misty has driven up onto charger guide
		private const double MISTY_ON_WEDGE_PITCH = 5;              // Degrees of pitch where we assume Misty has driven up onto charger guide
		private const double DOCKED_YAW_TOLERANCE = 5;              // How close we expected the final, docked Misty yaw to be compared to the expected value
		private const double IDEAL_ALIGNMENT_DISTANCE = 1.1;		// How far from the charger should we be (Z) for best alignment?
		private const double IDEAL_ALIGNMENT_OFFSET = 0.3;          // How far to the side (X meters) should we be from the charger when we 'get our bearings'
		private const double LOCATE_CHARGER_X_TOLERANCE = 0.01;		// How tightly we need to align with charger to 'get our bearings'
		private const double INITIAL_ROTATION_BEFORE_SWEEP = 35;    // When Misty first starts looking for charger, turn this far and then sweep in other direction
		private const double CHARGER_DOCK_OVERSHOOT = 0.2;			// How much further we backup on to charger beyond a perfect mount
		private const double SWEEP_STEP_DEGREES = 10;				// Sweep for charger step size
		private const int DOCKING_MAX_RETRIES = 3;                  // How many times to try complete docking process
		private const int ALIGN_MAX_RETRIES = 4;                    // How many times to try and align with charger per overall docking attempt
		private const int SHIMMY_RETRIES = 10;                      // How many times to try and shimy for final alignement on charger

		private const int DockingStationDetectorEnabled = 0x10000;

		#endregion

		#region Public Methods

		public ChargerDock(IRobotMessenger misty, SkillHelper skillHelper, double expectedFinalImuYaw)
		{
			_misty = misty ?? throw new ArgumentNullException(nameof(misty));
			_skillHelper = skillHelper ?? throw new ArgumentNullException(nameof(skillHelper));
			_expectedFinalImuYaw = expectedFinalImuYaw;

			_misty.RegisterBatteryChargeEvent(BatteryChargingMessage, 250, true, null, "BatteryChargingEvent", OnResponse);
		}

		public async Task<bool> DockAsync()
		{
			_skillHelper.LogMessage("Execute charger docking.");

			_previousChargerPose = new Queue<ChargerPosition>();
			_abort = false;
			_charging = false;

			// Pitch sometimes has an offset even when Misty is level.
			_initialPitch = AxisLimited(_skillHelper.ImuPitch);
			_skillHelper.LogMessage($"Initial pitch is {_initialPitch}.");

			_misty.RegisterChargerPoseEvent(ChargerPoseMessage, 100, true, "ChargerPoseEvent", OnResponse);
			if (!await StartDockDetectorAsync())
			{
				Cleanup();
				return false;
			}

			await _skillHelper.MoveHeadAsync(7, 0, 0);
			if (_abort) return false;

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
			// Align Misty directly in front of the charger facing towards the charger.
			int retries = 0;
			while (_chargerPose == null || Math.Abs(_chargerPose.X + CENTER_OFFSET) > ALIGNED_X || Math.Abs(_chargerPose.EulerYaw) > ALIGNED_EULER_YAW)
			{
				if (retries++ > ALIGN_MAX_RETRIES)
				{
					_skillHelper.LogMessage($"Unable to align with the charger after {retries} attempts.");
					Cleanup();
					return false;
				}
				else if (retries > 1)
				{
					_skillHelper.LogMessage("Insufficient aligment. Retrying.");

					await _skillHelper.TurnAsync(-90);
					if (_abort) return false;
					await _skillHelper.DriveAsync(IDEAL_ALIGNMENT_OFFSET);
					if (_abort) return false;
					await _skillHelper.TurnAsync(110);
					if (_abort) return false;
				}

				if (!await AlignWithChargerAsync())
				if (_abort) return false;
			}

			_skillHelper.LogMessage($"Charger position is [{_chargerPose.X}, {_chargerPose.Y}, {_chargerPose.Z}] meters. Euler yaw is {_chargerPose.EulerYaw} degrees.");

			// Turn around.
			_skillHelper.LogMessage("Turning 180 degrees to face away from charger.");
			double chargerDistance = _chargerPose.Z;
			await _skillHelper.TurnAsync(-180);
			if (_abort) return false;

			// Back on to charger.
			_skillHelper.LogMessage($"Driving {chargerDistance + CHARGER_DOCK_OVERSHOOT} meters to back on to charger.");
			await _skillHelper.DriveAsync(-chargerDistance - CHARGER_DOCK_OVERSHOOT, true);
			if (_abort) return false;

			// Check if we've ended up on top of the wedge.
			await Task.Delay(1000);
			_skillHelper.LogMessage($"Roll = {_skillHelper.ImuRoll}. Pitch = {_skillHelper.ImuPitch}.");
			if (Math.Abs(_skillHelper.ImuRoll) > MISTY_ON_WEDGE_ROLL || Math.Abs(AxisRotation(_initialPitch, _skillHelper.ImuPitch)) > MISTY_ON_WEDGE_PITCH)
			{
				// We're on the wedge. Drive away from the charger and try again.
				_skillHelper.LogMessage($"We appear to have driven on top of the alignment wedge. IMU roll is {_skillHelper.ImuRoll}. IMU pitch is {_skillHelper.ImuPitch}.");
				await _skillHelper.DriveAsync(.2);
				if (_abort) return false;
				await _skillHelper.TurnAsync(AxisRotation(_skillHelper.ImuYaw, _expectedFinalImuYaw));
				if (_abort) return false;
				await _skillHelper.DriveAsync(IDEAL_ALIGNMENT_DISTANCE - 0.1);
				if (_abort) return false;
				await _skillHelper.TurnAsync(180);
				_chargerPose = null;
				return false;
			}

			// Check that we're fully docked.
			if (!await ShimmyAsync(DOCKED_YAW_TOLERANCE))
			{
				// Shimmy failed. Drive away from the charger and try again.
				await _skillHelper.DriveAsync(.2);
				if (_abort) return false;
				await _skillHelper.TurnAsync(AxisRotation(_skillHelper.ImuYaw, _expectedFinalImuYaw));
				if (_abort) return false;
				await _skillHelper.DriveAsync(IDEAL_ALIGNMENT_DISTANCE - 0.1);
				if (_abort) return false;
				await _skillHelper.TurnAsync(180);
				return false;
			}

			return true;
		}

		private async Task<bool> AlignWithChargerAsync()
		{
			try
			{
				if (!await FindChargerAsync()) return false;

				// Turn to face directly at charger.
				_skillHelper.LogMessage("Turning to face directly at charger.");
				while (Math.Abs(_chargerPose.X) > LOCATE_CHARGER_X_TOLERANCE)
				{
					// If charger is to left of robot there will be a negative X which corresponds to a 
					// positive IMU based turn (CCW). So we invert bearing value here.
					var chargerBearing = -180.0 * Math.Atan(_chargerPose.X / _chargerPose.Z) / Math.PI;

					// Trying to make very small turns doesn't work. So we need to make larger step sizes.
					// We may need to overshoot and then come back and that's okay.
					if (Math.Abs(chargerBearing) < 3)
					{
						if (chargerBearing < 0)
							chargerBearing = -5;
						else
							chargerBearing = 5;
					}

					await _skillHelper.TurnAsync(chargerBearing);
					if (_abort) return false;
					await Task.Delay(1000);
					_skillHelper.LogMessage($"Charger position is [{_chargerPose.X}, {_chargerPose.Y}, {_chargerPose.Z}] meters. Euler yaw is {_chargerPose.EulerYaw} degrees. Turning {chargerBearing} degrees.");
				}

				// Calculate charger offset distance. This offset is the distance to the side that the charger would be if Misty
				// turned to have the same yaw as the charger. In other words, this is the distance that Misty needs to do 
				// drive perpendicular to the charger to be direcly in front of it.
				double chargerOffset = _chargerPose.Z * Math.Sin(Math.PI * _chargerPose.EulerYaw / 180.0);

				// Correct for Structure Core right camera offset from center of Misty.
				if (_chargerPose.EulerYaw > 0)
					chargerOffset -= CENTER_OFFSET;
				else
					chargerOffset += CENTER_OFFSET;
				_skillHelper.LogMessage($"Charger offset is {chargerOffset} meters.");

				// Turn perpendicular to charger.
				if (chargerOffset > 0)
				{
					// Charger is rotated CW so turn to the right.
					_skillHelper.LogMessage($"Turning {90 - _chargerPose.EulerYaw} degrees to be perpendicular to charger.");
					await _skillHelper.TurnAsync(90 - _chargerPose.EulerYaw);
				}
				else
				{
					// Charger is rotated CCW.
					_skillHelper.LogMessage($"Turning {-(90 + _chargerPose.EulerYaw)} degrees to be perpendicular to charger.");
					await _skillHelper.TurnAsync(-(90 + _chargerPose.EulerYaw));
				}
				if (_abort) return false;
				await Task.Delay(1000);

				// Drive to be aligned with charger.
				_skillHelper.LogMessage($"Driving {Math.Abs(chargerOffset)} meters to be directly in front of charger.");
				await _skillHelper.DriveAsync(Math.Abs(chargerOffset));
				if (_abort) return false;

				// Turn towards charger.
				_skillHelper.LogMessage($"Turning 90 degrees to face charger.");
				if (chargerOffset > 0)
					await _skillHelper.TurnAsync(-90);
				else
					await _skillHelper.TurnAsync(90);
				await Task.Delay(1000);

				// If needed, adjust distance from charger.
				if (_chargerPose.Z > IDEAL_ALIGNMENT_DISTANCE + .1 || IDEAL_ALIGNMENT_DISTANCE < IDEAL_ALIGNMENT_DISTANCE - .1)
				{
					_skillHelper.LogMessage($"Charger Z is {_chargerPose.Z}. Driving to {IDEAL_ALIGNMENT_DISTANCE} meters distance from charger for better data accuracy.");
					await _skillHelper.DriveAsync(_chargerPose.Z - IDEAL_ALIGNMENT_DISTANCE);
					await Task.Delay(1000);
				}

				_skillHelper.LogMessage($"Charger position is [{_chargerPose.X}, {_chargerPose.Y}, {_chargerPose.Z}] meters. Euler yaw is {_chargerPose.EulerYaw} degrees.");
			}
			catch (Exception ex)
			{
				_skillHelper.LogMessage("An exception occurred within ChargerDock.AlignWithChargerAsync: " + ex.Message);
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
					_skillHelper.LogMessage($"Charger at [{_chargerPose.X}, {_chargerPose.Y}, {_chargerPose.Z}] meters. Euler yaw is {_chargerPose.EulerYaw} degrees.");
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
						await _skillHelper.TurnAsync(AxisRotation(_skillHelper.ImuYaw, _expectedFinalImuYaw + 180));
						if (_abort) return false;

						TofValues tofValues = _skillHelper.GetTofValues();
						if (_abort) return false;
						double? distance = tofValues.FrontCenter.HasValue ? tofValues.FrontCenter.Value : tofValues.FrontLeft.HasValue ? tofValues.FrontLeft.Value : tofValues.FrontRight.Value;
						if (distance.HasValue)
						{
							await _skillHelper.DriveAsync(distance.Value - IDEAL_ALIGNMENT_DISTANCE);

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

				// Check for being in a reasonable location for starting alignment process.
				if (_chargerPose.Z < 0.8 * IDEAL_ALIGNMENT_DISTANCE)
				{
					_skillHelper.LogMessage($"Charger at [{_chargerPose.X}, {_chargerPose.Y}, {_chargerPose.Z}] meters. Euler yaw is {_chargerPose.EulerYaw} degrees.");
					_skillHelper.LogMessage("Too close to charger. Backing away.");
					await _skillHelper.TurnAsync(AxisRotation(_skillHelper.ImuYaw, _expectedFinalImuYaw + 180));
					await _skillHelper.DriveAsync(_chargerPose.Z - IDEAL_ALIGNMENT_DISTANCE);
				}

				_skillHelper.LogMessage($"Charger at [{_chargerPose.X}, {_chargerPose.Y}, {_chargerPose.Z}] meters. Euler yaw is {_chargerPose.EulerYaw} degrees.");
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

		public async Task<bool> ShimmyAsync(double dockedYawTolerance)
		{
			// 'Wiggle' until we are aligned with the expected yaw and we are charging.
			// It looks silly, but is simple works pretty reliably.
			// It can take several seconds for the charge state to update and so we need some long pauses.

			try
			{
				await Task.Delay(7000);
				_skillHelper.LogMessage($"Checking final alignment and adjusting position if necessary. IMU yaw is {_skillHelper.ImuYaw}. Expected IMU yaw is {_expectedFinalImuYaw}. Charging state is {_charging}.");

				int retries = 0;
				bool aligned = Math.Abs(AxisRotation(_skillHelper.ImuYaw, _expectedFinalImuYaw)) < dockedYawTolerance;
				while (!aligned || !_charging)
				{
					if (retries++ > SHIMMY_RETRIES)
					{
						_skillHelper.LogMessage($"Failed to shimmy successfully after {retries} attempts.");
						return false;
					}

					if (AxisRotation(_skillHelper.ImuYaw, _expectedFinalImuYaw) > 0)
						_misty.DriveArc(_skillHelper.ImuYaw - 10, 0, 300, false, OnResponse);
					else
						_misty.DriveArc(_skillHelper.ImuYaw + 10, 0, 300, false, OnResponse);
					await Task.Delay(1000);
					if (_abort) return false;

					_misty.DriveHeading(0, 0.15, 500, true, OnResponse);
					await Task.Delay(1000);
					if (_abort) return false;

					// Don't pause for charger state if we're not even aligned.
					aligned = Math.Abs(AxisRotation(_skillHelper.ImuYaw, _expectedFinalImuYaw)) < dockedYawTolerance;
					if (aligned && !_charging) await Task.Delay(7000);

					if (_abort) return false;

					_skillHelper.LogMessage($"IMU yaw is {_skillHelper.ImuYaw}. Expected IMU yaw is {_expectedFinalImuYaw}. Charging state is {_charging}.");
				}

				_skillHelper.LogMessage($"Shimmy complete. Expected IMU yaw is {_expectedFinalImuYaw}. Current IMU yaw is {_skillHelper.ImuYaw}. Charging state is {_charging}.");
			}
			catch (Exception ex)
			{
				_skillHelper.LogMessage("An exception occurred within ChargerDock.ShimmyAsync: " + ex.Message);
			}

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

			// TODO - have seen docking station detector fail to start even with the checking here.
			// Need to make more fool proof.

			int retries = 0;
			while (retries++ < 2 && (_slamStatus.Status & DockingStationDetectorEnabled) == 0)
			{
				_skillHelper.LogMessage("Dock detector did not start. Restarting SLAM service.");
				_misty.RebootSlamSensor(OnResponse);
				await Task.Delay(5000);

				_skillHelper.LogMessage("Start dock detector.");
				_misty.StartLocatingDockingStation(5000, 5000, OnResponse);
				await Task.Delay(3000);
			}

			_misty.UnregisterEvent("ChargerDockSlamStatus", OnResponse);

			if ((_slamStatus.Status & DockingStationDetectorEnabled) == 0)
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

	public class ChargerPosition
	{
		public float X { get; set; }
		public float Y { get; set; }
		public float Z { get; set; }
		public float EulerYaw { get; set; }
	}
}
