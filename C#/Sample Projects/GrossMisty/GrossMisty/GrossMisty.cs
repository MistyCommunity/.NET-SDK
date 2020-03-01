using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MistyRobotics.Common.Data;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Messengers;
using SkillTools.AssetTools;

namespace GrossMisty
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
	**********************************************************************/

	internal class GrossMisty : IMistySkill
	{
		private IRobotMessenger _misty;
		private Random _random = new Random();
		private IAssetWrapper _assetWrapper;
		private bool _reloadAssets;

		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("GrossMisty", "399eff6d-80c1-4ddc-8b76-8b74764d35f7")
		{
			AllowedCleanupTimeInMs = 4000,
			TimeoutInSeconds = int.MaxValue
		};

		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
			_assetWrapper = new AssetWrapper(_misty);
		}

		public async void OnStart(object sender, IDictionary<string, object> parameters)
		{
			ProcessParameters(parameters);
			await _assetWrapper.LoadAssets(_reloadAssets);

			//Attempts to run the C# MoveArmsAndHead skill in this repository
			_misty.RunSkill("7c7342f9-9155-4968-a8ea-82123c4c5282", null, null);

			_misty.RegisterBumpSensorEvent(BumpCallback, 0, true, null, null, null);
			while (_misty.Wait(_random.Next(8000, 15000)))
			{
				Ewww();
			}
		}
		
		private void Ewww()
		{
			int soundNumber = _random.Next(1, 5);
			switch(soundNumber)
			{
				case 1:
					_misty.PlayAudio("Fart-Common-Everyday-Fart_Mike-Koenig.wav", null, null);
					_assetWrapper.ShowSystemImage(SystemImage.Surprise);
					_misty.Wait(2000);
					_assetWrapper.PlaySystemSound(SystemSound.PhraseOopsy);
					break;
				case 2:
					_misty.PlayAudio("Quick Fart-SoundBible.com-655578646.wav", null, null);
					_assetWrapper.ShowSystemImage(SystemImage.JoyGoofy2);
					break;
				case 3:
					_misty.PlayAudio("Silly_Farts-Joe-1473367952.wav", null, null);
					_assetWrapper.ShowSystemImage(SystemImage.TerrorRight);
					_misty.Wait(3000);
					_assetWrapper.PlaySystemSound(SystemSound.PhraseUhOh);
					break;
				case 4:
					_misty.PlayAudio("Belch-Kevan-136688254.wav", null, null);
					_assetWrapper.ShowSystemImage(SystemImage.Disgust);
					_misty.Wait(3000);
					break;
			}
		}
		
		private void BumpCallback(IBumpSensorEvent bumpEvent)
		{
			//Only processing contacted events
			if (bumpEvent.IsContacted)
			{
				switch (bumpEvent.SensorPosition)
				{
					case BumpSensorPosition.FrontRight:
						_misty.PlayAudio("Silly_Farts-Joe-1473367952.wav", null, null);
						_assetWrapper.ShowSystemImage(SystemImage.JoyGoofy2);
						break;
					case BumpSensorPosition.FrontLeft:
						_misty.PlayAudio("Fart-Common-Everyday-Fart_Mike-Koenig.wav", null, null);
						_assetWrapper.ShowSystemImage(SystemImage.Disgust);
						break;
					case BumpSensorPosition.BackRight:
						_misty.PlayAudio("Quick Fart-SoundBible.com-655578646.wav", null, null);
						_assetWrapper.ShowSystemImage(SystemImage.Rage3);
						break;
					case BumpSensorPosition.BackLeft:
						_misty.PlayAudio("Belch-Kevan-136688254.wav", null, null);
						_assetWrapper.ShowSystemImage(SystemImage.Disgust);
						break;
				}
			}
		}

		private void ProcessParameters(IDictionary<string, object> parameters)
		{
			//Check to see if the robot should force the reloading of assets, if the asset name doesn't exist on the robot, it will load them
			KeyValuePair<string, object> reloadAssetsKVP = parameters.FirstOrDefault(x => x.Key.ToLower() == "reloadassets");
			if (reloadAssetsKVP.Value != null)
			{
				_reloadAssets = Convert.ToBoolean(reloadAssetsKVP.Value);
			}
		}

		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			OnComplete();
		}

		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			OnStart(sender, parameters);
		}
		
		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			OnComplete();
		}
		
		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			OnComplete();
		}

		private async void OnComplete()
		{
			_assetWrapper.PlaySystemSound(SystemSound.PhraseByeBye);
			_misty.CancelRunningSkill("7c7342f9-9155-4968-a8ea-82123c4c5282", null);

			//can't use _misty.Wait because cancellation token is triggered and it will return immediately
			await Task.Delay(3000);
			_misty.PlayAudio("Quick Fart-SoundBible.com-655578646.wav", null, null);
		}

		#region IDisposable Support
		private bool _isDisposed = false;

		private void Dispose(bool disposing)
		{
			if (!_isDisposed)
			{
				if (disposing)
				{
				}

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
