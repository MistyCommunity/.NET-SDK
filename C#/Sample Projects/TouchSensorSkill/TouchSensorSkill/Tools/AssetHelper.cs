using MistyRobotics.SDK.Messengers;

namespace TouchSensorSkill.Tools
{
	public sealed class AssetHelper
	{
		private IRobotMessenger _misty;

		public AssetHelper(IRobotMessenger robotMessenger)
		{
			_misty = robotMessenger;
		}

		/// <summary>
		/// Simple method wrapper to help with calling display system images
		/// </summary>
		/// <param name="systemImage"></param>
		public void ShowSystemImage(SystemImage systemImage)
		{
			_misty.DisplayImage($"e_{systemImage.ToString()}.jpg", null, false, null);
		}

		/// <summary>
		/// Simple method wrapper to help with calling display system images
		/// </summary>
		public void ShowSystemImage(SystemImage systemImage, double alpha)
		{
			_misty.DisplayImage($"e_{systemImage.ToString()}.jpg", null, false, null);
		}

		/// <summary>
		/// Simple method wrapper to help with calling play system audio
		/// </summary>
		public void PlaySystemSound(SystemSound sound)
		{
			_misty.PlayAudio($"s_{sound.ToString()}.wav", null, null);
		}

		/// <summary>
		/// Simple wrapper method to help with calling play system audio
		/// </summary>
		public void PlaySystemSound(SystemSound sound, int volume)
		{
			_misty.PlayAudio($"s_{sound.ToString()}.wav", volume, null);
		}
	}
}