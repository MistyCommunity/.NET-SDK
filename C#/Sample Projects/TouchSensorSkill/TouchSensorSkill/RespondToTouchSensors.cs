using System.Collections.Generic;
using MistyRobotics.Common.Data;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;
using TouchSensorSkill.Tools;
using System;

namespace TouchSensorSkill
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

	internal class RespondToTouchSensors : IMistySkill
	{
		private IRobotMessenger _misty;

		private AssetHelper _assetHelper;

		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("RespondToTouchSensors", "24c5eae9-3a04-4c4a-96ad-31cbe58afb87")
		{
			AllowedCleanupTimeInMs = 1000,
			TimeoutInSeconds = int.MaxValue
		};
		
		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
			_assetHelper = new AssetHelper(_misty);
		}

		public void OnStart(object sender, IDictionary<string, object> parameters)
		{
			_misty.TransitionLED(255, 140, 0, 0, 0, 255, LEDTransition.Breathe, 1000, null);
			_misty.Wait(3000);
			_misty.ChangeLED(0, 0, 255, null);

			//Register Bump Sensors with a callback
			_misty.RegisterBumpSensorEvent(BumpCallback, 0, true, null, null, null);

			//Register Cap Touch with a callback
			_misty.RegisterCapTouchEvent(CapTouchCallback, 0, true, null, null, null);
		}

		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			_misty.TransitionLED(0, 0, 255, 255, 0, 0, LEDTransition.TransitOnce, 2000, null);
		}

		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			_misty.TransitionLED(0, 0, 255, 255, 140, 0, LEDTransition.TransitOnce, 2000, null);
		}

		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			//No special cleanup needed in this skill
		}

		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			OnStart(sender, parameters);
		}
		
		private void CapTouchCallback(ICapTouchEvent capTouchEvent)
		{
			//Only processing contacted events
			if (capTouchEvent.IsContacted)
			{
				switch (capTouchEvent.SensorPosition)
				{
					case CapTouchPosition.Back:
						_assetHelper.PlaySystemSound(SystemSound.Amazement);
						_assetHelper.ShowSystemImage(SystemImage.Amazement);
						break;
					case CapTouchPosition.Front:
						_assetHelper.PlaySystemSound(SystemSound.PhraseHello);
						_assetHelper.ShowSystemImage(SystemImage.ContentRight);
						break;
					case CapTouchPosition.Right:
						_assetHelper.PlaySystemSound(SystemSound.Joy);
						_assetHelper.ShowSystemImage(SystemImage.Joy2);
						break;
					case CapTouchPosition.Left:
						_assetHelper.PlaySystemSound(SystemSound.Love);
						_assetHelper.ShowSystemImage(SystemImage.Love);
						break;
					case CapTouchPosition.Scruff:
						_assetHelper.PlaySystemSound(SystemSound.PhraseEvilAhHa);
						_assetHelper.ShowSystemImage(SystemImage.Rage);
						break;
					case CapTouchPosition.Chin:
						_assetHelper.PlaySystemSound(SystemSound.Sleepy);
						_assetHelper.ShowSystemImage(SystemImage.Sleepy);
						break;
				}
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
						_assetHelper.PlaySystemSound(SystemSound.PhraseEvilAhHa);
						_assetHelper.ShowSystemImage(SystemImage.Anger);
						break;
					case BumpSensorPosition.FrontLeft:
						_assetHelper.PlaySystemSound(SystemSound.PhraseUhOh);
						_assetHelper.ShowSystemImage(SystemImage.Fear);
						break;
					case BumpSensorPosition.BackRight:
						_assetHelper.PlaySystemSound(SystemSound.PhraseOwwww);
						_assetHelper.ShowSystemImage(SystemImage.Disgust);
						break;
					case BumpSensorPosition.BackLeft:
						_assetHelper.PlaySystemSound(SystemSound.Boredom);
						_assetHelper.ShowSystemImage(SystemImage.DefaultContent);
						break;
				}
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
			GC.SuppressFinalize(this);
		}

		~RespondToTouchSensors()
		{
			Dispose(false);
		}

		#endregion
	}
}
