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
using Windows.Foundation;

namespace MistyNavigation
{
	/// <summary>
	/// Simple type expressing the location and orientation of an object in 2D.
	/// Easier for most people to understand this than the 16 element, 3D pose matrix.
	/// Uses standard Misty coordinate system:
	/// X is forward.
	/// Y is to the left.
	/// Yaw is positive counter-clockwise in radians.
	/// </summary>
	public struct Simple2DPose
	{
		public double X;
		public double Y;
		public double Yaw;
		public override string ToString()
		{
			return $"[{X:f3}, {Y:f3}, {Yaw.Degrees():f0}]";
		}
	}

	/// <summary>
	/// Encapsulates a sequence of steps to move from A to B.
	/// Turn1 followed by DriveDistance followed by Turn2.
	/// </summary>
	public struct MoveSequence
	{
		public double Turn1;
		public double DriveDistance;
		public double Turn2;
	}

	public static class NavigationHelper
	{
		/// <summary>
		/// Converts a 16 element 3D pose matrix in Occipital coordinates to a SimplePose
		/// </summary>
		public static Simple2DPose ConvertPose(float[] pose)
		{
			var simplePose = new Simple2DPose()
			{
				X = pose[14],
				Y = -pose[12],
				Yaw = Math.Asin(pose[2])
			};

			return simplePose;
		}

		/// <summary>
		/// Returns the location of object 1 in object 2's coordinate system given the location of object 2 in object 1's coordinate system.
		/// </summary>
		public static Point SwapCoordinateSystems(Simple2DPose object2Pose)
		{
			// Translate the coordinate system.
			var object1PoseTranslated = new Point()
			{
				X = -object2Pose.X,
				Y = -object2Pose.Y
			};

			// Rotate the coordinate system.
			// https://en.wikipedia.org/wiki/Rotation_matrix
			double rotation = Math.PI - object2Pose.Yaw;
			var object1Pose = new Point()
			{
				X = object1PoseTranslated.X * Math.Cos(rotation) - object1PoseTranslated.Y * Math.Sin(rotation),
				Y = object1PoseTranslated.X * Math.Sin(rotation) + object1PoseTranslated.Y * Math.Cos(rotation)
			};

			return object1Pose;
		}

		/// <summary>
		/// Given the pose of a target from two different locations, determine the MoveSequence required to move
		/// from position1 to position2.
		/// </summary>
		public static MoveSequence CalculateMoveSequence(Simple2DPose targetFromPosition1, Simple2DPose targetFromPosition2)
		{
			System.Diagnostics.Debug.WriteLine("Target from position1: " + targetFromPosition1.ToString());
			System.Diagnostics.Debug.WriteLine("Target from position2: " + targetFromPosition2.ToString());

			// Get the coordinates of both positions in the target's coordinate system.
			Point position1FromTarget = SwapCoordinateSystems(targetFromPosition1);
			Point position2FromTarget = SwapCoordinateSystems(targetFromPosition2);

			System.Diagnostics.Debug.WriteLine($"Position 1 from target: [{position1FromTarget.X:f3}, {position1FromTarget.Y:f3}]");
			System.Diagnostics.Debug.WriteLine($"Position 2 from target: [{position2FromTarget.X:f3}, {position2FromTarget.Y:f3}]");

			// Get the movement needed from position 1 to position 2 in the target's coordinate system.
			var moveInTargetCoordinates = new Point()
			{
				X = position2FromTarget.X - position1FromTarget.X,
				Y = position2FromTarget.Y - position1FromTarget.Y
			};
			System.Diagnostics.Debug.WriteLine($"Movement needed in target coordinate system: [{moveInTargetCoordinates.X:f3}, {moveInTargetCoordinates.Y:f3}]");

			// Rotate the movement back to position 1's coordinate system.
			double rotation = Math.PI + targetFromPosition1.Yaw;
			var moveInPosition1Coordinates = new Point()
			{
				X = moveInTargetCoordinates.X * Math.Cos(rotation) - moveInTargetCoordinates.Y * Math.Sin(rotation),
				Y = moveInTargetCoordinates.X * Math.Sin(rotation) + moveInTargetCoordinates.Y * Math.Cos(rotation)
			};
			System.Diagnostics.Debug.WriteLine($"Movement needed in position 1's coordinate system: [{moveInPosition1Coordinates.X:f3}, {moveInPosition1Coordinates.Y:f3}]");

			// Get distance and bearing from position 1 to position 2 in position 1's coordinate system.
			double distanceFromPosition1ToPosition2 = Math.Sqrt(Math.Pow(moveInPosition1Coordinates.X, 2) + Math.Pow(moveInPosition1Coordinates.Y, 2));

			double bearingFromPosition1ToPosition2;
			if (moveInPosition1Coordinates.X == 0)
			{
				if (moveInPosition1Coordinates.Y > 0)
				{
					// Moving directly to the left
					bearingFromPosition1ToPosition2 = Math.PI / 2.0;
				}
				else
				{
					// Moving directly to the right
					bearingFromPosition1ToPosition2 = -Math.PI / 2.0;
				}
			}
			else if (moveInPosition1Coordinates.X > 0)
			{
				// Moving towards target
				bearingFromPosition1ToPosition2 = Math.Atan(moveInPosition1Coordinates.Y / moveInPosition1Coordinates.X);
			}
			else
			{
				// Moving away from target
				bearingFromPosition1ToPosition2 = Math.PI + Math.Atan(moveInPosition1Coordinates.Y / moveInPosition1Coordinates.X);
			}
			bearingFromPosition1ToPosition2 = NormalizeTurn(bearingFromPosition1ToPosition2);
			System.Diagnostics.Debug.WriteLine($"Bearing from position 1 to position 2: {bearingFromPosition1ToPosition2.Degrees():f0}");

			// Determine the final turn needed to face the target after moving to position 2.
			var targetFromPosition2InPosition1Coordinates = new Point()
			{
				X = targetFromPosition1.X - moveInPosition1Coordinates.X,
				Y = targetFromPosition1.Y - moveInPosition1Coordinates.Y
			};

			targetFromPosition2InPosition1Coordinates.X = targetFromPosition2InPosition1Coordinates.X == 0 ? 0.00001 : targetFromPosition2InPosition1Coordinates.X;
			double faceTarget = -bearingFromPosition1ToPosition2 + Math.Atan(targetFromPosition2InPosition1Coordinates.Y / targetFromPosition2InPosition1Coordinates.X);
			faceTarget = NormalizeTurn(faceTarget);

			// Adjust face target turn so we end with the desired orientation.
			targetFromPosition2.X = targetFromPosition2.X == 0 ? 0.00001 : targetFromPosition2.X;
			double finalTurn = faceTarget - Math.Atan(targetFromPosition2.Y / targetFromPosition2.X);
			finalTurn = NormalizeTurn(finalTurn);

			return new MoveSequence()
			{
				Turn1 = bearingFromPosition1ToPosition2,
				DriveDistance = distanceFromPosition1ToPosition2,
				Turn2 = finalTurn
			};
		}

		public static double Degrees(this double radians)
		{
			return 180.0 * radians / Math.PI;
		}

		public static double Radians(this double degrees)
		{
			return Math.PI * degrees / 180.0;
		}

		public static double NormalizeTurn(double radians)
		{
			// Normalize angle to be between -PI and PI.
			double norm = radians % (2 * Math.PI);
			if (norm > Math.PI)
				norm = norm - 2 * Math.PI;
			else if (norm < -Math.PI)
				norm = 2 * Math.PI + norm;

			return norm;
		}
	}
}
