using System;
using MistyRobotics.SDK.Messengers;
using WanderSkill.Types;

namespace WanderSkill
{	
	internal abstract class BaseDrive
	{
		protected IRobotMessenger _misty;

		protected CurrentObstacleState _currentObstacleState;

		protected Random _randomGenerator = new Random();

		protected LocomotionStatus _locomotionStatus = LocomotionStatus.Unknown;

		protected bool _debugMode;

		public BaseDrive(IRobotMessenger robot, CurrentObstacleState currentObstacleState, bool debugMode)
		{
			_misty = robot;
			_currentObstacleState = currentObstacleState;
			_debugMode = debugMode;
		}

		private void HandeDebugLocomotionMode()
		{
			if(_debugMode)
			{
				switch(_locomotionStatus)
				{
					case LocomotionStatus.DrivingBackward:
						_misty.ChangeLED(255, 240, 0, null);
						break;
					case LocomotionStatus.DrivingForward:
						_misty.ChangeLED(0, 255, 0, null);
						break;
					case LocomotionStatus.DrivingForwardLeft:
						_misty.ChangeLED(0, 125, 255, null);
						break;
					case LocomotionStatus.DrivingForwardRight:
						_misty.ChangeLED(0, 255, 127, null);
						break;
					case LocomotionStatus.Stopped:
						_misty.ChangeLED(255, 0, 0, null);
						break;
					case LocomotionStatus.TurningLeft:
						_misty.ChangeLED(255, 255, 0, null);
						break;
					case LocomotionStatus.TurningRight:
						_misty.ChangeLED(255, 0, 255, null);
						break;
					default:
						//Unknown
						_misty.ChangeLED(255, 255, 255, null);
						break;
				}
			}
		}

		private void SetLocomotionStatus(double linearVelocity, double angularVelocity, double difference = 0)
		{
			if (linearVelocity == 0 && angularVelocity == 0)
			{
				_locomotionStatus = LocomotionStatus.Stopped;
			}
			else if (linearVelocity == 0)
			{
				if (difference < 0)
				{
					_locomotionStatus = LocomotionStatus.TurningRight;
				}
				else
				{
					_locomotionStatus = LocomotionStatus.TurningLeft;
				}
			}
			else if (linearVelocity > 0 && angularVelocity == 0)
			{
				_locomotionStatus = LocomotionStatus.DrivingForward;
			}
			else if (linearVelocity < 0)
			{
				_locomotionStatus = LocomotionStatus.DrivingBackward;
			}
			else if (difference < 0)
			{
				_locomotionStatus = LocomotionStatus.DrivingForwardRight;
			}
			else
			{
				_locomotionStatus = LocomotionStatus.DrivingForwardLeft;
			}

			HandeDebugLocomotionMode();
		}

		protected void SendDriveCommand(double linearVelocity, double angularVelocity, double difference = 0, int? driveTimeMs = null)
		{
			//Should keep angular positive, difference tells how to turn
			angularVelocity = Math.Abs(angularVelocity); 
			if (difference < 0)
			{
				//turn right
				angularVelocity = -angularVelocity;
			}

			SetLocomotionStatus(linearVelocity, angularVelocity, difference);

			if (driveTimeMs != null && driveTimeMs > 0)
			{
				_misty.DriveTime(linearVelocity, angularVelocity, (int)driveTimeMs, null);
			}
			else
			{
				_misty.Drive(linearVelocity, angularVelocity, null);
			}
		}

		public abstract void Drive();
	}
}