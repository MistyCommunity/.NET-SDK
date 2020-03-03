﻿using System.Collections.Generic;
using System.Threading.Tasks;
using MistyRobotics.Common.Data;
using Windows.Storage;

namespace SkillTools.AssetTools
{
	/// <summary>
	/// Asset wrapper to help with asset specific capabilities
	/// </summary>
	public interface IAssetWrapper
	{
		/// <summary>
		/// Current Image List 
		/// May be slightly incorrect if new images have been pushed to robot from another source
		/// </summary>
		IList<ImageDetails> ImageList { get; }

		/// Current Audio List 
		/// May be slightly incorrect if new audio files have been pushed to robot from another source
		IList<AudioDetails> AudioList { get; }

		/// Current video List 
		/// May be slightly incorrect if new videos have been pushed to robot from another source
		IList<VideoDetails> VideoList { get; }

		/// <summary>
		/// Will request the asset lists from the robot and update the skills info
		/// </summary>
		/// <returns></returns>
		Task RefreshAssetLists();

		/// <summary>
		/// Simple method wrapper to help with displaying system images
		/// </summary>
		/// <param name="systemImage"></param>
		void ShowSystemImage(SystemImage systemImage);
		
		/// <summary>
		/// Simple method wrapper to help with displaying system images
		/// </summary>
		/// <param name="systemImage"></param>
		/// <param name="layer"></param>
		void ShowSystemImage(SystemImage systemImage, string layer);

		/// <summary>
		/// Simple method wrapper to help with playing system audio
		/// </summary>
		void PlaySystemSound(SystemSound sound);

		/// <summary>
		/// Simple wrapper method to help with playing system audio
		/// </summary>
		void PlaySystemSound(SystemSound sound, int volume);

		/// <summary>
		/// Attempts to load image, video and audio assets in the project's 'Assets/SkillAssets' folder, or the overridden storage folder, to the robot's system
		/// </summary>
		/// <param name="forceReload">force the system to upload all assets, whether they exist or not</param>
		/// <param name="assetFolder">pass in to override the default location</param>
		/// <returns></returns>
		Task LoadAssets(bool forceReload = false, StorageFolder assetFolder = null);
	}
}