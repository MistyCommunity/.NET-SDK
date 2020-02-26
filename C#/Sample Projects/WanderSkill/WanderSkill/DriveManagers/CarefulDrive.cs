using MistyRobotics.SDK.Messengers;
using WanderSkill.Types;

namespace WanderSkill.DriveManagers
{
	internal class CarefulDrive : BaseDrive
	{
		//Drives slowly back and forth in a smaller space - occasionally spins
		public CarefulDrive(IRobotMessenger robot, CurrentObstacleState wanderState, bool debugMode)
			: base(robot, wanderState, debugMode) { }

		public override void Drive()
		{
			//Turn every once in a while (if we weren't just turning)
			if (_locomotionStatus != LocomotionStatus.TurningLeft && _locomotionStatus != LocomotionStatus.TurningRight && _randomGenerator.Next(1, 5) == 1)
			{
				var angular = _randomGenerator.Next(20, 30);
				if (_randomGenerator.Next(1, 4) == 1)
				{
					angular = -angular;
				}
				SendDriveCommand(0, angular, 0, 2000);
			}
			else //drive back and forth
			{
				var linear = _randomGenerator.Next(10, 20);
				var angular = _randomGenerator.Next(0, 15);
				if (_randomGenerator.Next(1, 3) == 1)
				{
					angular = -angular;
				}

				if (_locomotionStatus == LocomotionStatus.DrivingForward || 
						_locomotionStatus == LocomotionStatus.DrivingForwardLeft || 
						_locomotionStatus == LocomotionStatus.DrivingForwardRight ||
						_locomotionStatus == LocomotionStatus.Stopped)
				{
					//We were driving forward (or were stopped), so try to drive backwards now
					linear = -linear;
				}
				
				SendDriveCommand(linear, angular, 0, 3000);
			}
		}
	}
}