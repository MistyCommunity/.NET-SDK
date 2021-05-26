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
using System.Threading.Tasks;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Responses;

namespace MistyNavigation
{
	// This class adds to the 'follow path' system by providing a mechanism for Misty to automatically
	// align to an AR tag at one more locations along a path. The recipe step contains the distance and yaw
	// of the AR tag from Misty's desired location.
	// Misty coordinates are: 
	//   X is straight ahead in meters.
	//   Y is to the left in meters.
	//   Yaw is positive rotation counter-clockwise in radians.
	public class ArTagAligner
	{
		// Alignment tolerances in meters and degrees.
		private const double X_TOLERANCE = 0.05;
		private const double Y_TOLERANCE = 0.03;
		private const double YAW_TOLERANCE = 3;

		private readonly IRobotMessenger _misty;
		private readonly SkillHelper _skillHelper;

		private bool _abort;
		private int _tagId;
		private Simple2DPose _currentTagPose;

		public ArTagAligner(IRobotMessenger misty, SkillHelper skillHelper)
		{
			_misty = misty ?? throw new ArgumentNullException(nameof(misty));
			_skillHelper = skillHelper ?? throw new ArgumentNullException(nameof(skillHelper));

			Cleanup(); // just in case things weren't cleaned up during the previous run
		}

		public async Task<bool> AlignToArTag(int dictionary, double size, int tagId, Simple2DPose goalTagPose)
		{
			_abort = false;
			_tagId = tagId;
			_skillHelper.LogMessage($"Starting AR tag alignment. Desired tag pose is [x, y, yaw] {goalTagPose.X:f3}, {goalTagPose.Y:f3}, {goalTagPose.Yaw.Degrees():f0}].");

			// Look straight ahead
			await _skillHelper.MoveHeadAsync(0, 0, 0);

			// Startup AR tag detection
			_misty.EnableCameraService(OnResponse);
			_misty.EnableCameraService(OnResponse); // Calling twice to make sure. TODO: something smarter
			_misty.StartArTagDetector(dictionary, size, OnResponse);
			_misty.StartArTagDetector(dictionary, size, OnResponse); // Calling twice to make sure. TODO: something smarter
			_misty.RegisterArTagDetectionEvent(ArTagCallback, 100, true, "artagevent", OnResponse);

			// Wait for measurments to start.
			if (! await WaitForMeasurementAsync(10, 2))
			{
				if (_abort) return false;

				// Never detected the AR tag. Try a small sweep.
				_skillHelper.MistySpeak("I can't see the tag. Looking around for it.");
				_skillHelper.LogMessage("Do not see the tag. Starting sweep.");
				bool arTagFound = false;
				await _skillHelper.TurnAsync(20);
				int maxTurns = 5;
				while (!arTagFound && --maxTurns >= 0)
				{
					arTagFound = await WaitForMeasurementAsync(1, 1);
					if (_abort) return false;
					if (!arTagFound)
					{
						await _skillHelper.TurnAsync(-10);
					}
				}

				if (!arTagFound)
				{
					_skillHelper.MistySpeak("I cannot find the tag.");
					_skillHelper.LogMessage("Never detected the AR tag.");
					Cleanup();
					return false;
				}
			}

			_skillHelper.LogMessage($"AR tag located at [x, y, yaw] : [{_currentTagPose.X:f3}, {_currentTagPose.Y:f3}, {_currentTagPose.Yaw.Degrees():f1}]. " +
				$"Offset : [{(_currentTagPose.X - goalTagPose.X):f3}, {(_currentTagPose.Y - goalTagPose.Y):f3}, {(_currentTagPose.Yaw.Degrees() - goalTagPose.Yaw.Degrees()):f1}].");

			if (Math.Abs(_currentTagPose.X - goalTagPose.X) < X_TOLERANCE && Math.Abs(_currentTagPose.Y - goalTagPose.Y) < Y_TOLERANCE &&
				Math.Abs(_currentTagPose.Yaw.Degrees() - goalTagPose.Yaw.Degrees()) < YAW_TOLERANCE)
			{
				_skillHelper.MistySpeak("We're already aligned with the tag. No need to move.");
				_skillHelper.LogMessage("No movement needed to align to tag.");
			}
			else
			{
				int retries = 0;
				while (Math.Abs(_currentTagPose.X - goalTagPose.X) > X_TOLERANCE || Math.Abs(_currentTagPose.Y - goalTagPose.Y) > Y_TOLERANCE ||
					Math.Abs(_currentTagPose.Yaw.Degrees() - goalTagPose.Yaw.Degrees()) > YAW_TOLERANCE)
				{
					MoveSequence moveSequence = NavigationHelper.CalculateMoveSequence(_currentTagPose, goalTagPose);
					_skillHelper.LogMessage($"Movement sequence to goal is turn {moveSequence.Turn1.Degrees():f0} degrees, drive {moveSequence.DriveDistance:f3} meters, " +
						$"and turn {moveSequence.Turn2.Degrees():f0} degrees.");

					await _skillHelper.TurnAsync(moveSequence.Turn1.Degrees());
					if (_abort) return false;

					await _skillHelper.DriveAsync(moveSequence.DriveDistance, true);
					if (_abort) return false;

					await _skillHelper.TurnAsync(moveSequence.Turn2.Degrees());
					if (_abort) return false;

					_skillHelper.LogMessage($"AR tag located at [x, y, yaw] : [{_currentTagPose.X:f3}, {_currentTagPose.Y:f3}, {_currentTagPose.Yaw.Degrees():f1}]. " +
						$"Offset : [{(_currentTagPose.X - goalTagPose.X):f3}, {(_currentTagPose.Y - goalTagPose.Y):f3}, {(_currentTagPose.Yaw.Degrees() - goalTagPose.Yaw.Degrees()):f1}].");

					if (retries++ > 5)
					{
						_skillHelper.LogMessage("Failed to line up with the tag.");
						_skillHelper.MistySpeak("I can't seem to line up right. I give up.");
						await Task.Delay(5000);
						Cleanup();
						return false;
					}
				}

				_skillHelper.MistySpeak("Alignment complete.");
			}

			Cleanup();

			return true;
		}

		public void Abort()
		{
			Cleanup();
			_abort = true;
		}

		// Wait maxSeconds seconds for readingCount AR tag measurements.
		private async Task<bool> WaitForMeasurementAsync(int maxSeconds, int readingCount)
		{
			int count = 0;
			bool wait = true;
			_currentTagPose.X = 0;

			DateTime start = DateTime.Now;
			while (wait)
			{
				if (_abort) return false;

				await Task.Delay(100);
				if (DateTime.Now.Subtract(start).TotalSeconds > maxSeconds)
				{
					_skillHelper.LogMessage("Failed to detect AR tag.");
					return false;
				}

				if (_currentTagPose.X != 0)
				{
					count++;
				}
				if (count == readingCount)
				{
					wait = false;
				}
				else
				{
					_currentTagPose.X = 0;
				}
			}

			return true;
		}

		private void ArTagCallback(IArTagDetectionEvent e)
		{
			if (e.TagId == _tagId)
			{
				float[] matrix = e.ArTagPose.HomogeneousMatrix;
				//_skillHelper.LogMessage($"AR tag pose: [{matrix[0]}, {matrix[1]}, {matrix[2]}, {matrix[3]}, {matrix[4]}, {matrix[5]}, {matrix[6]}, {matrix[7]}, {matrix[8]}, {matrix[9]}, {matrix[10]}, {matrix[11]}, {matrix[12]}, {matrix[13]}, {matrix[14]}, {matrix[15]}]");
	
				_currentTagPose = NavigationHelper.ConvertPose(matrix);
				//_skillHelper.LogMessage("AR tag pose: " + _currentTagPose.ToString());
			}
		}

		private void OnResponse(IRobotCommandResponse commandResponse)
		{

		}

		private void Cleanup()
		{
			_skillHelper.LogMessage("AR tag aligner cleanup.");

			_misty.StopArTagDetector(OnResponse);
			_misty.UnregisterEvent("artagevent", OnResponse);
		}
	}
}
