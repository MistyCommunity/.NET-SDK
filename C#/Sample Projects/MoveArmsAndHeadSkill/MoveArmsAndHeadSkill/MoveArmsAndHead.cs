using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MistyRobotics.Common.Data;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;

namespace MoveArmsAndHeadSkill
{
	/**********************************************************************
		Copyright 2020 Misty Robotics
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


	internal class MoveArmsAndHead : IMistySkill
	{
		private const int MinimumArmDegreesInclusive = -90;
		private const int MaximumArmDegreesExclusive = 91;
		private const int MinimumArmSpeedInclusive = 30;
		private const int MaximumArmSpeedExclusive = 101;

		private const int MinimumPitchDegreesInclusive = -40;
		private const int MaximumPitchDegreesExclusive = 16;
		private const int MinimumRollDegreesInclusive = -30;
		private const int MaximumRollDegreesExclusive = 31;		
		private const int MinimumYawDegreesInclusive = -70;
		private const int MaximumYawDegreesExclusive = 71;
		private const int MinimumHeadSpeedInclusive = 25;
		private const int MaximumHeadSpeedExclusive = 76;

		private IRobotMessenger _misty;		
		private Timer _moveHeadTimer;
		private int _moveHeadPauseInMilliseconds = 5000;
		private Timer _moveArmsTimer;		
		private int _moveArmPauseInMilliseconds = 2500;
		private Random _randomGenerator = new Random();

		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("MoveArmsAndHead", "7c7342f9-9155-4968-a8ea-82123c4c5282")
		{
			AllowedCleanupTimeInMs = 1000,
			TimeoutInSeconds = int.MaxValue
		};

		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
		}

		public void OnStart(object sender, IDictionary<string, object> parameters)
		{
			_misty.TransitionLED(255, 140, 0, 0, 0, 255, LEDTransition.Breathe, 1000, null);
			_misty.Wait(3000);
			_misty.ChangeLED(0, 0, 255, null);

			ProcessParameters(parameters);

			//Randomly moves the head every X ms
			_moveHeadTimer = new Timer(MoveHeadCallback, null, 0, _moveHeadPauseInMilliseconds);

			//Randomly moves the arms every X ms
			_moveArmsTimer = new Timer(MoveArmCallback, null, 0, _moveArmPauseInMilliseconds);
		}

		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			OnCancel(sender, parameters);
		}

		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			OnStart(sender, parameters);
		}

		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			_misty.TransitionLED(0, 0, 255, 255, 0, 0, LEDTransition.TransitOnce, 2000, null);
		}

		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			_misty.TransitionLED(0, 0, 255, 255, 140, 0, LEDTransition.TransitOnce, 2000, null);
		}

		public void MoveHeadCallback(object timerData)
		{
			_misty.MoveHead(_randomGenerator.Next(MinimumPitchDegreesInclusive, MaximumPitchDegreesExclusive), _randomGenerator.Next(MinimumRollDegreesInclusive, MaximumRollDegreesExclusive), _randomGenerator.Next(MinimumYawDegreesInclusive, MaximumYawDegreesExclusive), _randomGenerator.Next(MinimumHeadSpeedInclusive, MaximumHeadSpeedExclusive), AngularUnit.Degrees, null);
		}
		
		public void MoveArmCallback(object timerData)
		{
			_misty.MoveArms(_randomGenerator.Next(MinimumArmDegreesInclusive, MaximumArmDegreesExclusive), _randomGenerator.Next(MinimumArmDegreesInclusive, MaximumArmDegreesExclusive), _randomGenerator.Next(MinimumArmSpeedInclusive, MaximumArmSpeedExclusive), _randomGenerator.Next(MinimumArmSpeedInclusive, MaximumArmSpeedExclusive), null, AngularUnit.Degrees, null);
		}

		private void ProcessParameters(IDictionary<string, object> parameters)
		{
			try
			{
				KeyValuePair<string, object> moveHeadKVP = parameters.FirstOrDefault(x => x.Key.ToLower().Trim() == "moveheadpause");
				if (moveHeadKVP.Value != null)
				{
					int moveHeadPause = Convert.ToInt32(moveHeadKVP.Value);
					if (moveHeadPause > 100)//min pause
					{
						_moveHeadPauseInMilliseconds = moveHeadPause;
					}
				}

				KeyValuePair<string, object> moveArmKVP = parameters.FirstOrDefault(x => x.Key.ToLower().Trim() == "movearmpause");
				if (moveArmKVP.Value != null)
				{
					int moveArmPause = Convert.ToInt32(moveArmKVP.Value);
					if(moveArmPause > 100)//min pause
					{
						_moveArmPauseInMilliseconds = moveArmPause;
					}
				}
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log("Failed handling startup parameters", ex);
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
					_moveArmsTimer?.Dispose();
					_moveHeadTimer?.Dispose();
				}
				
				_isDisposed = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~MoveArmsAndHead()
		{
			Dispose(false);
		}

		#endregion
	}
}