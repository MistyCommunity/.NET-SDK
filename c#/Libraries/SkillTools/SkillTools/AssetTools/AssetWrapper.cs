using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MistyRobotics.Common.Data;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Responses;
using Windows.Storage.Streams;
using Windows.Storage;

namespace SkillTools.AssetTools
{
	public sealed class AssetWrapper : IAssetWrapper
	{
		private IRobotMessenger _misty;
		public IList<ImageDetails> ImageList { get; private set; } = new List<ImageDetails>();
		public IList<AudioDetails> AudioList { get; private set; } = new List<AudioDetails>();
		public IList<VideoDetails> VideoList { get; private set; } = new List<VideoDetails>();

		public async Task RefreshAssetLists()
		{
			//Get the current assets on the robot
			IGetAudioListResponse audioListResponse = await _misty.GetAudioListAsync();
			if (audioListResponse.Status == ResponseStatus.Success && audioListResponse.Data.Count() > 0)
			{
				AudioList = audioListResponse.Data;
			}

			IGetImageListResponse imageListResponse = await _misty.GetImageListAsync();
			if (imageListResponse.Status == ResponseStatus.Success && imageListResponse.Data.Count() > 0)
			{
				ImageList = imageListResponse.Data;
			}

			IGetVideoListResponse videoListResponse = await _misty.GetVideoListAsync();
			if (videoListResponse.Status == ResponseStatus.Success && videoListResponse.Data.Count() > 0)
			{
				VideoList = videoListResponse.Data;
			}
		}

		public AssetWrapper(IRobotMessenger robotMessenger)
		{
			_misty = robotMessenger;
		}

		/// <summary>
		/// Simple method wrapper to help with displaying system images
		/// </summary>
		/// <param name="systemImage"></param>
		public void ShowSystemImage(SystemImage systemImage)
		{
			_misty.DisplayImage($"e_{systemImage.ToString()}.jpg", null, false, null);
		}

		/// <summary>
		/// Simple method wrapper to help with displaying system images
		/// </summary>
		/// <param name="systemImage"></param>
		/// <param name="layer"></param>
		public void ShowSystemImage(SystemImage systemImage, string layer)
		{
			_misty.DisplayImage($"e_{systemImage.ToString()}.jpg", layer, false, null);
		}
		
		/// <summary>
		/// Simple method wrapper to help with playing system audio
		/// </summary>
		public void PlaySystemSound(SystemSound sound)
		{
			_misty.PlayAudio($"s_{sound.ToString()}.wav", null, null);
		}

		/// <summary>
		/// Simple wrapper method to help with playing system audio
		/// </summary>
		public void PlaySystemSound(SystemSound sound, int volume)
		{
			_misty.PlayAudio($"s_{sound.ToString()}.wav", volume, null);
		}

		/// <summary>
		/// Attempts to load image, video and audio assets in the project's 'Assets/SkillAssets' folder, or the overridden storage folder, to the robot's system
		/// </summary>
		/// <param name="forceReload">force the system to upload all assets, whether they exist or not</param>
		/// <param name="assetFolder">pass in to override the default location</param>
		/// <returns></returns>
		public async Task LoadAssets(bool forceReload = false, StorageFolder assetFolder = null)
		{
			try
			{
				await RefreshAssetLists();

				//Load the assets in the Assets/SkillAssets folder to the robot if they are missing or if ReloadAssets is passed in
				StorageFolder skillAssetFolder = assetFolder ?? await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync(@"Assets\SkillAssets");
				IList<StorageFile> assetFileList = (await skillAssetFolder.GetFilesAsync()).ToList();
				foreach (StorageFile storageFile in assetFileList)
				{
					if (forceReload ||
						(!AudioList.Any(x => x.Name == storageFile.Name) &&
						!ImageList.Any(x => x.Name == storageFile.Name) &&
						!VideoList.Any(x => x.Name == storageFile.Name)))
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
							if ((await _misty.SaveAudioAsync(storageFile.Name, contents, false, true)).Status == ResponseStatus.Success)
							{
								AudioList.Add(new AudioDetails { Name = storageFile.Name, SystemAsset = false });
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
								VideoList.Add(new VideoDetails { Name = storageFile.Name, SystemAsset = false });
								_misty.SkillLogger.LogInfo($"Uploaded video asset '{storageFile.Name}'");
							}
							else
							{
								_misty.SkillLogger.Log($"Failed to upload video asset '{storageFile.Name}'");
							}
						}
						else if (storageFile.Name.EndsWith(".jpg") ||
							storageFile.Name.EndsWith(".jpeg") ||
							storageFile.Name.EndsWith(".png") ||
							storageFile.Name.EndsWith(".gif"))
						{
							if ((await _misty.SaveImageAsync(storageFile.Name, contents, false, true, 0, 0)).Status == ResponseStatus.Success)
							{
								ImageList.Add(new ImageDetails { Name = storageFile.Name, SystemAsset = false });
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
			catch(Exception ex)
			{
				_misty.SkillLogger.Log("Error loading assets.", ex);

			}
		}
	}
}