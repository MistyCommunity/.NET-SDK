using MistyRobotics.SDK.Messengers;
using WanderSkill.Types;

namespace WanderSkill.DriveManagers
{
	/// <summary>
	/// Uses the Time of Flights to wander around
	/// </summary>
	internal class WanderDrive : BaseDrive
	{
		public WanderDrive(IRobotMessenger robot, CurrentObstacleState wanderState, bool debugMode)
			: base(robot, wanderState, debugMode) {}

		public override void Drive()
		{		
			//Back up if edge sensors triggered
			if (_currentObstacleState.FrontRightEdgeTOF > 0.05 || _currentObstacleState.FrontLeftEdgeTOF > 0.05)
			{
				int backupTime = _randomGenerator.Next(1000, 2000);
				SendDriveCommand(-20, 0, 0);
				_misty.Wait(backupTime);

				int turnTime = _randomGenerator.Next(2500, 3000);
				SendDriveCommand(0, _randomGenerator.Next(20, 25), _currentObstacleState.FrontLeftEdgeTOF > _currentObstacleState.FrontRightEdgeTOF ? 1 : -1);
				_misty.Wait(turnTime);
				return;
			}

			var closestFrontDistance = _currentObstacleState.FrontRightTOF < _currentObstacleState.FrontLeftTOF ? _currentObstacleState.FrontRightTOF : _currentObstacleState.FrontLeftTOF;
			closestFrontDistance = closestFrontDistance < _currentObstacleState.FrontCenterTOF ? closestFrontDistance : _currentObstacleState.FrontCenterTOF;

			var LRDifference = _currentObstacleState.FrontLeftTOF - _currentObstacleState.FrontRightTOF;

			_misty.SkillLogger.LogVerbose($"ClosestFrontDistance = {closestFrontDistance} - LRDifference = {LRDifference} - FrontRightTOF = {_currentObstacleState.FrontRightTOF} - FrontLeftTOF = {_currentObstacleState.FrontLeftTOF}");

			if (_currentObstacleState.FrontRightBumpContacted || _currentObstacleState.FrontLeftBumpContacted)
			{
				SendDriveCommand(-15, 30, LRDifference);
				_misty.Wait(750);
			}

			if (_currentObstacleState.BackRightBumpContacted || _currentObstacleState.BackLeftBumpContacted)
			{
				SendDriveCommand(15, 30, LRDifference);
				_misty.Wait(750);
			}

			//TODO Update for m/s driving vs percent driving
			//TODO Update for transition of speeds
			//TODO add hazards, imu, and other locomotion information
			if (closestFrontDistance < 0.1)
			{
				//Randomly turn once in a while instead of time of flight driving
				if (_randomGenerator.Next(1, 4) == 1)
				{
					SendDriveCommand(-15, 50, LRDifference);
					_misty.Wait(1500);

					var angular = _randomGenerator.Next(25, 35);
					SendDriveCommand(0, angular, LRDifference);
					_misty.Wait(1000);
				}
				else
				{
					SendDriveCommand(-20, 30, LRDifference);
					_misty.Wait(2000);
				}
			}
			else if (closestFrontDistance < 0.225)
			{
				if (_randomGenerator.Next(1, 5) == 1)
				{
					var angular = _randomGenerator.Next(20, 30);
					SendDriveCommand(0, angular, LRDifference);
					_misty.Wait(1500);
				}
				
				//Randomly do other stuff once in a while
				if (_randomGenerator.Next(1, 5) == 1)
				{
					var angular = _randomGenerator.Next(20, 40);
					SendDriveCommand(-5, angular, LRDifference);
					_misty.Wait(1500);
				}
				else
				{
					SendDriveCommand(-15, 30, LRDifference);
					_misty.Wait(500);
				}
			}
			else
			{
				double angular = 0;
				double linear = 60;

				if (closestFrontDistance < 0.25)
				{
					angular = 80;
					linear = 5;
				}
				else if (closestFrontDistance < 0.3)
				{
					angular = 70;
					linear = 10;
				}
				else if (closestFrontDistance < 0.5)
				{
					angular = 60;
					linear = 20;
				}
				else if (closestFrontDistance < 0.6)
				{
					angular = 50;
					linear = 30;
				}
				else if (closestFrontDistance < 0.7)
				{
					angular = 40;
					linear = 35;
				}
				else if (closestFrontDistance < 0.8)
				{
					angular = 20;
					linear = 45;
				}
				else if (closestFrontDistance < 0.9)
				{
					angular = 10;
					linear = 50;
				}
				else if (closestFrontDistance < 1)
				{
					linear = 55;
				}

				//Don't go from driving back or stopped to driving forward at too high a speed
				if ((_locomotionStatus == LocomotionStatus.Stopped || 
					_locomotionStatus == LocomotionStatus.DrivingBackward) 
					&& linear >= 40)
				{
					linear = linear / 2;
				}

				SendDriveCommand(linear, angular, LRDifference);
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
		}
		#endregion
	}
}
