using System;
using System.Collections.Generic;
using System.Linq;
using MistyRobotics.Common.Data;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;
using Newtonsoft.Json;

namespace TextToSpeechSkill
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
	
	internal class TextToSpeech : IMistySkill
	{
		private IRobotMessenger _misty;

		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("TextToSpeechSkill", "32fad23d-074c-4267-8377-7fd60da380d3");

		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
		}

		private async void TTSCallback(IUserEvent userEvent)
		{
			IDictionary<string, object> payloadData = JsonConvert.DeserializeObject<Dictionary<string, object>>(userEvent.Data["Payload"].ToString());
			string text = Convert.ToString(payloadData.FirstOrDefault(x => x.Key == "Text").Value);
			if (!string.IsNullOrWhiteSpace(text))
			{
				await _misty.SpeakAsync(text, true, null);
				await _misty.DisplayTextAsync(text, null);
			}
		}

		public async void OnStart(object sender, IDictionary<string, object> parameters)
		{
			await _misty.SpeakAsync("Hello!", true, null);
			await _misty.DisplayTextAsync("Hello!", null);
			_misty.RegisterUserEvent("Speak", TTSCallback, 0, true, null);
		}

		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			OnCancel(sender, parameters);
		}

		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			OnStart(sender, parameters);
		}
		
		public async void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			await _misty.SpeakAsync("Goodbye!", true, null);
			await _misty.SetDisplaySettingsAsync(true);
		}

		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			OnCancel(sender, parameters);
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
