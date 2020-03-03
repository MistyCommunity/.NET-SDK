using System;

namespace WanderSkill
{
	internal class CurrentObstacleState
	{
		public double FrontLeftTOF { get; set; }

		public double FrontRightTOF { get; set; }

		public double FrontCenterTOF { get; set; }

		public double BackTOF { get; set; }

		public bool BackLeftBumpContacted { get; set; }

		public bool BackRightBumpContacted { get; set; }

		public bool FrontLeftBumpContacted { get; set; }

		public bool FrontRightBumpContacted { get; set; }

		public double FrontRightEdgeTOF { get; set; }

		public double FrontLeftEdgeTOF { get; set; }

		public double BackRightEdgeTOF { get; set; }

		public double BackLeftEdgeTOF { get; set; }
	}
}