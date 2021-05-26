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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MistyRobotics.Common.Data;
using MistyRobotics.SDK.Responses;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;
using MistyNavigation;

namespace DriveAndDock
{
	internal class MistySkill : IMistySkill
	{
		private const string DefaultRecipe = "path1.txt";

		private IRobotMessenger _misty;
		private SkillHelper _skillHelper;
		private FollowPath _followPath;

		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("DriveAndDock", "b4075ac6-a9a5-4519-a7c5-4ae6cceb8f79");

		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			Skill.TimeoutInSeconds = int.MaxValue;
			_misty = robotInterface;
		}

		public void OnStart(object sender, IDictionary<string, object> parameters)
		{
			Task.Run(async () =>
			{
				// Just in case things weren't cleaned up last time.
				_misty.UnregisterAllEvents(OnResponse);
				await Task.Delay(2000); // Let unregister complete before starting to register for events

				_skillHelper = new SkillHelper(_misty);
				_skillHelper.MistySpeak("Initiating prototype path following skill.");
				_skillHelper.LogMessage("Initiating prototype path following skill v0.0.10.");

				// So she doesn't look dead
				await _skillHelper.MoveHeadAsync(0, 0, 0);

				// Sometimes event messages don't start flowing. So wait and check for that before starting to run.
				// Hopefully this logic is temporary.
				DateTime start = DateTime.Now;
				while((_skillHelper.LastEncoderMessageReceived < start || _skillHelper.LastImuMessageReceived < start) && 
					DateTime.Now.Subtract(start).TotalSeconds < 15)
				{
					await Task.Delay(500);
				}
				if(_skillHelper.LastEncoderMessageReceived > start && _skillHelper.LastImuMessageReceived > start)
				{
					// Load the path to follow.
					string recipeName = DefaultRecipe;
					string key = parameters.Keys.FirstOrDefault(k => k.ToUpper() == "RECIPE");
					if (!string.IsNullOrWhiteSpace(key))
					{
						recipeName = parameters[key].ToString();
					}

					_followPath = new FollowPath(_misty, _skillHelper);
					await Task.Delay(2000); // Let any internal cleanup complete before running.

					List<IFollowPathCommand> commands = await _followPath.LoadCommandsAsync(recipeName, MyDelegateAsync);

					// Follow the path
					await _followPath.Execute(commands);
				}
				else
				{
					_skillHelper.LogMessage($"IMU and/or encoder messages never started flowing. Last encoder message: {_skillHelper.LastEncoderMessageReceived}. " +
						$"Last IMU message: {_skillHelper.LastImuMessageReceived}.");
					_skillHelper.MistySpeak("I am not receiving IMU and encoder messages as expected. Unable to execute the recipe.");
				}

				Cleanup();
			});
		}

		// Delegate:x in the recipe file will result in this method being called with argument = x
		private async Task<bool> MyDelegateAsync(string argument)
		{
			_skillHelper.LogMessage("Executing delegate.");

			// Your code here. Whatever you want.
			await _misty.ChangeLEDAsync(50, 100, 100);
			
			return true;
		}

		private void Cleanup()
		{
			_followPath?.Abort();
			_skillHelper?.Abort();

			_misty.UnregisterAllEvents(OnResponse);
			_misty.SkillCompleted();
		}

		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			
		}

		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			
		}
		
		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			Cleanup();
		}

		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			Cleanup();
		}

		public void OnResponse(IRobotCommandResponse response)
		{
			
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

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~MistySkill() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}
