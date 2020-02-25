//////////////////////////////////////////////////////////////////////////////////////////
//
// This is sample prototype quality Misty skill code that represents one way to achieve certain
// functionality. Use as you wish.
//
//////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading.Tasks;
using MistyRobotics.Common.Data;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Responses;

namespace MistySkillTypes
{
	/// <summary>
	/// Loads an Occipital map, relocalizes in the map, and drives to the specified location.
	/// </summary>
	public class MapNav
	{
		#region Private Members

		private readonly IRobotMessenger _misty;
		private readonly SkillHelper _skillHelper;
		private readonly double _headPitchOffset = 0;
		private readonly double _headRollOffset = -5;
		private readonly double _headYawOffset = -2;

		private bool _abort;
		private SlamStatusDetails _slamStatus;
		private MapCell _mapCell;
		private double _mapYaw;
		private bool _tracking;

		#endregion

		#region Public Methods

		public MapNav(IRobotMessenger misty, SkillHelper skillHelper, double headPitchOffset, double headRollOffset, double headYawOffset)
		{
			_misty = misty ?? throw new ArgumentNullException(nameof(misty));
			_skillHelper = skillHelper ?? throw new ArgumentNullException(nameof(skillHelper));
			_headPitchOffset = headPitchOffset;
			_headRollOffset = headRollOffset;
			_headYawOffset = headYawOffset;
		}

		public async Task<bool> StartTrackingAsync(string mapName)
		{
			_skillHelper.LogMessage($"Attempting to track within map {mapName}.");
			_abort = false;
			
			// We don't get the newly loaded map back from Occ software until we start and stop tracking. So...
			_skillHelper.LogMessage("Loading map and starting tracking.");
			_misty.SetCurrentSlamMap(mapName, OnResponse);
			await Task.Delay(1000);
			_misty.StartTracking(OnResponse);

			await _skillHelper.MoveHeadAsync(_headPitchOffset, _headRollOffset, _headYawOffset);
			_misty.RegisterSlamStatusEvent(SlamStatusCallback, 0, true, "MapDockSlamStatusEvent", null, OnResponse);
			_misty.RegisterSelfStateEvent(SelfStateCallback, 250, true, "MapDockSelfStateEvent", OnResponse);

			// There is a current defect where switching maps does not fully take affect until we start tracking
			// and stop tracking. So we need to start, stop, and re-start.
			_misty.StopTracking(OnResponse);
			await Task.Delay(4000);
			_misty.StartTracking(OnResponse);
			await Task.Delay(4000);
			_misty.GetMap(OnResponse);
			await Task.Delay(4000);

			if (_slamStatus == null || _slamStatus.SensorStatus != MistyRobotics.Common.Types.SlamSensorMode.Streaming)
			{
				_skillHelper.LogMessage("Failed to start tracking.");
				Cleanup();
				return false;
			}

			_skillHelper.LogMessage("Checking for pose.");
			int count = 0;
			while (_slamStatus.RunMode != MistyRobotics.Common.Types.SlamRunningMode.Tracking && count++ < 40)
			{
				await _skillHelper.TurnAsync(10);
				_misty.GetSlamStatus(OnResponse);
			}

			if (_slamStatus.RunMode == MistyRobotics.Common.Types.SlamRunningMode.Tracking)
			{
				_skillHelper.LogMessage($"Pose acquired. Map cell is [{_mapCell.X},{_mapCell.Y}]. Map yaw is {_mapYaw:f2}.");
				_tracking = true;
			}
			else
			{
				_skillHelper.LogMessage("Unable to obtain pose.");
				Cleanup();
				_tracking = false;
			}

			return _tracking;
		}

		public async Task<bool> MoveToAsync(double x, double y, double yaw, double tolerance)
		{
			if (!_tracking) return false;

			// Get bearing to the destination
			double bearing = 0;
			if (_mapCell.X > x)
			{
				if (_mapCell.Y > y)
				{
					bearing = -90 - 180 * Math.Atan((_mapCell.X - x) / (_mapCell.Y - y)) / Math.PI;
				}
				else
				{
					bearing = 90 + 180 * Math.Atan((_mapCell.X - x) / (y - _mapCell.Y)) / Math.PI;
				}
			}
			else
			{
				bearing = 180 * Math.Atan((y - _mapCell.Y) / (x - _mapCell.X)) / Math.PI;
			}
			_skillHelper.LogMessage($"Bearing to destination is {bearing:f2}.");

			// Turn towards destination
			await _skillHelper.TurnAsync(bearing - _mapYaw);
			if (_abort) return false;
			_skillHelper.LogMessage($"Map cell is [{_mapCell.X},{_mapCell.Y}]. Map yaw is {_mapYaw:f2}.");

			// Get distance to destination and drive there.
			// Note: assuming constant 0.04 meters per cell.
			double distance = 0.04 * Math.Sqrt(Math.Pow(_mapCell.X - x, 2) + Math.Pow(_mapCell.Y - y, 2));
			await _skillHelper.DriveAsync(distance);
			if (_abort) return false;
			_skillHelper.LogMessage($"Map cell is [{_mapCell.X},{_mapCell.Y}]. Map yaw is {_mapYaw:f2}.");

			// Turn to desired yaw
			double delta = yaw - _mapYaw;
			while (Math.Abs(delta) > 1)
			{
				// Can't turn less than 3 degrees.
				if (Math.Abs(delta) < 3)
				{
					if (delta < 0)
						delta = -3;
					else
						delta = 3;
				}
				await _skillHelper.TurnAsync(delta);
				if (_abort) return false;
				await Task.Delay(500);
				delta = yaw - _mapYaw;
				_skillHelper.LogMessage($"Map cell is [{_mapCell.X},{_mapCell.Y}]. Map yaw is {_mapYaw:f2}.");
			}

			delta = y - _mapCell.Y;
			while (Math.Abs(delta) > tolerance)
			{
				await _skillHelper.TurnAsync(90);
				await _skillHelper.DriveAsync(0.04 * delta);
				await _skillHelper.TurnAsync(-90);
				_skillHelper.LogMessage($"Map cell is [{_mapCell.X},{_mapCell.Y}]. Map yaw is {_mapYaw:f2}.");
				delta = y - _mapCell.Y;
				if (_abort) return false;
			}

			return true;
		}

		public void Abort()
		{
			_abort = true;
			_skillHelper.LogMessage("MapNav processing aborted.");
			Cleanup();
		}

		public void Cleanup()
		{
			_skillHelper.LogMessage("MapNav cleanup.");

			_misty.StopTracking(OnResponse);
			_misty.UnregisterEvent("MapDockSlamStatusEvent", OnResponse);
			_misty.UnregisterEvent("MapDockSelfStateEvent", OnResponse);
		}

		#endregion

		#region Private Methods

		private void SlamStatusCallback(ISlamStatusEvent eventResponse)
		{
			_slamStatus = eventResponse.SlamStatus;
			_skillHelper.LogMessage($"SlamStatus is {_slamStatus.RunMode}, {_slamStatus.SensorStatus}, {_slamStatus.Status}.");
		}

		private void SelfStateCallback(ISelfStateEvent eventResponse)
		{
			_mapCell = eventResponse.MapCell;
			_mapYaw = 180 * eventResponse.Pose.Yaw / Math.PI;
			//_skillHelper.LogMessage($"Map cell is [{_mapCell.X},{_mapCell.Y}].");
		}

		private void OnResponse(IRobotCommandResponse commandResponse)
		{
			//_skillHelper.LogMessage($"Command response status is {commandResponse.Status}.");
		}

		#endregion
	}
}
