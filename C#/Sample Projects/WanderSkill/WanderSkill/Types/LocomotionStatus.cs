namespace WanderSkill.Types
{
	/// <summary>
	/// Current driving direction of the robot
	/// </summary>
	public enum LocomotionStatus
	{
		Unknown = 0,
		DrivingForward = 1,
		DrivingBackward = 2,
		DrivingForwardRight = 3,
		DrivingForwardLeft = 4,
		TurningRight = 5,
		TurningLeft = 6,
		Stopped = 7
	}
}