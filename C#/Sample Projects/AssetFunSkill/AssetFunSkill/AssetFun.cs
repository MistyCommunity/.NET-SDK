using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MistyRobotics.Common.Data;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK.Responses;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.Tools.DataStorage;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AssetFunSkill
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

	internal class AssetFun : IMistySkill
	{
		private IRobotMessenger _misty;
		private ISkillStorage _skillStorage;
		private bool _reloadAssets;
		private Timer _playSoundTimer;
		private IList<ImageDetails> _imageList = new List<ImageDetails>();		
		private IList<AudioDetails> _audioList = new List<AudioDetails>();
		private IList<VideoDetails> _videoList = new List<VideoDetails>();
		private char[] _textDelimiters = new char[] { ' ', '.', ',', '!', '?' };
		private Random _random = new Random();

		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("AssetFunSkill", "b270e022-8859-4a80-a829-0a3f4977e68e")
		{
			AllowedCleanupTimeInMs = 2000,
			TimeoutInSeconds = int.MaxValue
		};
		
		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
		}
		
		public async void OnStart(object sender, IDictionary<string, object> parameters)
		{
			ProcessParameters(parameters);
			await _misty.EnableAudioServiceAsync();

			_misty.TransitionLED(255, 140, 0, 0, 0, 255, LEDTransition.Breathe, 500, null);
			_misty.Wait(3000);
			_misty.ChangeLED(0, 0, 255, null);

			//Simple example of database use to track number of runs
			await LogNumberOfRuns();

			//Load assets if they do not exist on the robot or if reloadAssets sent in
			await LoadAssets();

			//Example of using a timer to play an uploaded sound every X ms
			_playSoundTimer = new Timer(PlaySoundCallback, null, 0, 15000);

			//Example of kicking off a side thread for the text display
			_ = Task.Run(() => DisplayTextLoop());
			
			//Example of setting a display setting and looping through simple display changes...
			await _misty.SetImageDisplaySettingsAsync
			(
				"AssetFunLayer2",
				new ImageSettings
				{
					Stretch = ImageStretch.Fill
				}
			);

			//_misty.Wait(milliseconds) will wait (approx.) that amount of time
			//if the skill is cancelled, it will return immediately with a false boolean response
			while (_misty.Wait(5000))
			{
				await _misty.DisplayImageAsync("AssetFunSkillPumpkin1.jpg", "AssetFunLayer2", false);
				_misty.Wait(5000);
				await _misty.DisplayImageAsync("AssetFunSkillPumpkin2.jpg", "AssetFunLayer2", false);				
			}
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
			_misty.TransitionLED(0, 0, 255, 255, 0, 0, LEDTransition.TransitOnce, 2000, null);
			await _misty.SetDisplaySettingsAsync(true);
		}
		
		public async void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			_misty.TransitionLED(0, 0, 255, 255, 140, 0, LEDTransition.TransitOnce, 2000, null);
			await _misty.SetDisplaySettingsAsync(true);
		}

		private async void PlaySoundCallback(object timerData)
		{
			await _misty.PlayAudioAsync("AssetFunSkillFoghorn.mp3", null);
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

		private async Task LogNumberOfRuns()
		{
			try
			{
				int numRuns = 0;
				_skillStorage = SkillStorage.GetDatabase(Skill);

				//Keep track of the number of times this is run in the file
				IDictionary<string, object> storedData = await _skillStorage.LoadDataAsync();
				if (storedData != null && storedData.ContainsKey("NumberOfRuns"))
				{
					numRuns = Convert.ToInt32(storedData["NumberOfRuns"]);
				}

				await _skillStorage.SaveDataAsync(new Dictionary<string, object> { { "NumberOfRuns", ++numRuns } });
			}
			catch(Exception ex)
			{
				_misty.SkillLogger.Log("Failed to update run number in data store.", ex);
			}
		}

		private async Task LoadAssets()
		{
			//Get the current assets on the robot
			IGetAudioListResponse audioListResponse = await _misty.GetAudioListAsync();
			if (audioListResponse.Status == ResponseStatus.Success && audioListResponse.Data.Count() > 0)
			{
				_audioList = audioListResponse.Data;
			}

			IGetImageListResponse imageListResponse = await _misty.GetImageListAsync();
			if (imageListResponse.Status == ResponseStatus.Success && imageListResponse.Data.Count() > 0)
			{
				_imageList = imageListResponse.Data;
			}

			IGetVideoListResponse videoListResponse = await _misty.GetVideoListAsync();
			if (videoListResponse.Status == ResponseStatus.Success && videoListResponse.Data.Count() > 0)
			{
				_videoList = videoListResponse.Data;
			}

			//Load the assets in the Assets/SkillAssets folder to the robot if they are missing or if ReloadAssets is passed in
			StorageFolder skillAssetFolder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync(@"Assets\SkillAssets");
			IList<StorageFile> assetFileList = (await skillAssetFolder.GetFilesAsync()).ToList();
			foreach (StorageFile storageFile in assetFileList)
			{
				if(_reloadAssets || 
					(!_audioList.Any(x => x.Name == storageFile.Name) && 
					!_imageList.Any(x => x.Name == storageFile.Name) &&
					!_videoList.Any(x => x.Name == storageFile.Name)))
				{
					StorageFile file = await skillAssetFolder.GetFileAsync(storageFile.Name);
					IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();
					byte[] contents = new byte[stream.Size];
					await stream.AsStream().ReadAsync(contents, 0, contents.Length);

					if (storageFile.Name.EndsWith(".mp3") ||
						storageFile.Name.EndsWith(".wav") ||
						storageFile.Name.EndsWith(".wma") ||
						storageFile.Name.EndsWith(".aac"))
					{
						if((await _misty.SaveAudioAsync(storageFile.Name, contents, false, true)).Status == ResponseStatus.Success)
						{
							_audioList.Add(new AudioDetails { Name = storageFile.Name, SystemAsset = false });
							_misty.SkillLogger.LogInfo($"Uploaded audio asset '{storageFile.Name}'");
						}
						else
						{
							_misty.SkillLogger.Log($"Failed to upload audio asset '{storageFile.Name}'");
						}
					}
					else if (storageFile.Name.EndsWith(".mp4") ||
						storageFile.Name.EndsWith(".wmv"))
					{
						if ((await _misty.SaveVideoAsync(storageFile.Name, contents, false, true)).Status == ResponseStatus.Success)
						{
							_videoList.Add(new VideoDetails { Name = storageFile.Name, SystemAsset = false });
							_misty.SkillLogger.LogInfo($"Uploaded video asset '{storageFile.Name}'");
						}
						else
						{
							_misty.SkillLogger.Log($"Failed to upload video asset '{storageFile.Name}'");
						}
					}
					else if(storageFile.Name.EndsWith(".jpg") ||
						storageFile.Name.EndsWith(".jpeg") ||
						storageFile.Name.EndsWith(".png") ||
						storageFile.Name.EndsWith(".gif"))
					{
						if ((await _misty.SaveImageAsync(storageFile.Name, contents, false, true, 0, 0)).Status == ResponseStatus.Success)
						{
							_imageList.Add(new ImageDetails { Name = storageFile.Name, SystemAsset = false });
							_misty.SkillLogger.LogInfo($"Uploaded image asset '{storageFile.Name}'");
						}
						else
						{
							_misty.SkillLogger.Log($"Failed to upload image asset '{storageFile.Name}'");
						}
					}
					else
					{
						_misty.SkillLogger.Log($"Unknown extension for asset '{storageFile.Name}', could not load to robot.");
					}
				}				
			}			
		}

		private async void DisplayTextLoop()
		{
			while (_misty.Wait(500))
			{
				await WriteMe("Hello! I hope you enjoy our jack-o-lanterns!", "AssetFunLayer1");
			}
		}
		
		private async Task<int> WriteMe(string textToWrite, string layer, string stringDelimiters = " ,!.?", int delayMilliseconds = 500)
		{
			//Set a random-ish display layer
			await _misty.SetTextDisplaySettingsAsync
			(
				layer,
				new TextSettings
				{
					Weight = _random.Next(600, 1001),
					Blue = (byte)_random.Next(0, 256),
					Red = (byte)_random.Next(0, 256),
					Green = (byte)_random.Next(0, 256),
					Size = _random.Next(70, 100),
					VerticalAlignment = ImageVerticalAlignment.Bottom,
					Style = ImageStyle.Italic,
					FontFamily = "Calibri",
					HorizontalAlignment = ImageHorizontalAlignment.Center,
					Wrap = true,
					PadTop = _random.Next(180, 220),
					Opacity = 1,
					PlaceOnTop = true,
					Rotation = _random.Next(5, 11),
					Visible = true
				}
			);

			string[] stringArray = Regex.Split(textToWrite, $@"(?<=[{stringDelimiters}])");
			foreach (string text in stringArray)
			{
				_misty.DisplayText(text, layer, null);
				if(!_misty.Wait(delayMilliseconds))
				{
					return stringArray.Length;
				}
			}

			return stringArray.Length;
		}

		#region IDisposable Support

		private bool _isDisposed = false;

		private void Dispose(bool disposing)
		{
			if (!_isDisposed)
			{
				if (disposing)
				{
					_playSoundTimer?.Dispose();
				}
				
				_isDisposed = true;
			}
		}

		~AssetFun()
		{
			Dispose(false);
		}
		
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		
		#endregion
	}
}
