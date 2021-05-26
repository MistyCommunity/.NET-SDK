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
using System.Threading.Tasks;
using MistyRobotics.SDK.Messengers;
using Windows.Storage;

namespace MistyNavigation
{
	public class FollowPath
	{
		#region Private Members

		private readonly IRobotMessenger _misty;
		private readonly SkillHelper _skillHelper;
		private readonly ArTagAligner _arTagAligner;
		private readonly ChargerDockSmall _docker;

		private bool _abort;

		#endregion

		#region Public Methods

		public FollowPath(IRobotMessenger misty, SkillHelper skillHelper)
		{
			_misty = misty ?? throw new ArgumentNullException(nameof(misty));
			_skillHelper = skillHelper ?? throw new ArgumentNullException(nameof(skillHelper));

			_arTagAligner = new ArTagAligner(_misty, _skillHelper);
			_docker = new ChargerDockSmall(_misty, _skillHelper);
		}

		// Sequentially invoke each command in collection provided.
		public async Task<bool> Execute(List<IFollowPathCommand> commands)
		{
			_abort = false;
			bool success = true;

			// Disabling for now as we get too many false positives.
			_skillHelper.DisableHazardSystem();

			foreach (IFollowPathCommand cmd in commands)
			{
				if (cmd is DriveCommand)
				{
					success = await _skillHelper.DriveAsync(((DriveCommand)cmd).Meters);
				}
				else if (cmd is TurnCommand)
				{
					success = await _skillHelper.TurnAsync(((TurnCommand)cmd).Degrees);
				}
				else if (cmd is ARTagAlignment)
				{
					ARTagAlignment arCmd = (ARTagAlignment)cmd;
					var goalTagPose = new Simple2DPose()
					{
						X = arCmd.X,
						Y = arCmd.Y,
						Yaw = arCmd.Yaw.Radians()
					};
					success = await _arTagAligner.AlignToArTag(arCmd.Dictionary, arCmd.Size, arCmd.TagId, goalTagPose);
				}
				else if (cmd is DelegateCommand)
				{
					success = await((DelegateCommand)cmd).Delegate.Invoke(((DelegateCommand)cmd).Argument);
				}
				else if (cmd is DockCommand)
				{
					success = await _docker.DockAsync();
				}

				if (_abort || !success) break;
			}

			_skillHelper.EnableHazardSystem();

			return success;
		}

		public void Abort()
		{
			_abort = true;
			_arTagAligner.Abort();
			_docker.Abort();
		}

		/// <summary>
		/// Loads a sequence of recipe commands from a the specified file within the \Data\Misty\SDK\Recipes\ folder
		/// </summary>
		public async Task<List<IFollowPathCommand>> LoadCommandsAsync(string filename, Func<string, Task<bool>> delegateCommand)
		{
			var commands = new List<IFollowPathCommand>();

			try
			{
				StorageFolder folder = await StorageFolder.GetFolderFromPathAsync("C:\\Data\\Misty\\SDK\\Recipes");
				StorageFile file = await folder.GetFileAsync(filename);
				var lines = await FileIO.ReadLinesAsync(file);

				foreach (string line in lines)
				{
					if (line.Length > 1 && !line[0].Equals('#'))
					{
						string cmd = line.ToUpper();
						string arg = "";
						string[] split = line.Split(':');
						if (split.Length == 2)
						{
							cmd = split[0].ToUpper();
							arg = split[1].ToUpper();
						}
						switch (cmd)
						{
							case "DRIVE":
								double.TryParse(arg, out double distance);
								commands.Add(new DriveCommand(distance));
								break;
							case "TURN":
								double.TryParse(arg, out double degrees);
								commands.Add(new TurnCommand(degrees));
								break;
							case "ARTAG":
								string[] art = arg.Split(',');
								if (art.Length == 6)
								{
									int.TryParse(art[0], out int dictionary);
									double.TryParse(art[1], out double size);
									int.TryParse(art[2], out int tagId);
									double.TryParse(art[3], out double x);
									double.TryParse(art[4], out double y);
									double.TryParse(art[5], out double yaw);
									commands.Add(new ARTagAlignment(dictionary, size, tagId, x, y, yaw));

									// WARN AGAINST RISKY CONFIGURATIONS
									if (size < 100 && x > 1)
									{
										_skillHelper.MistySpeak($"Warning. You are expecting to align to a tag of size {size} at over one meter away. This is not a reliable operation.");
										await Task.Delay(6000);
									}
									if (x > 2)
									{
										_skillHelper.MistySpeak($"Warning. You are expecting to align to a tag that is over two meters away. This is not a reliable operation.");
										await Task.Delay(6000);
									}
								}
								break;
							case "DELEGATE":
								commands.Add(new DelegateCommand(delegateCommand, arg));
								break;
							case "DOCK":
								commands.Add(new DockCommand());
								break;
						}
					}
				}
			}
			catch (Exception ex)
			{
				_skillHelper.LogMessage($"Failed to read commands file, {filename}: {ex.Message}.");
				_skillHelper.MistySpeak("I could not read the recipe file.");
			}

			return commands;
		}

		#endregion

		#region Private Methods


		#endregion
	}

	// *******************************************************************
	// Class for each each command type
	public interface IFollowPathCommand
	{
	}

	public class DriveCommand : IFollowPathCommand
	{
		public double Meters { get; set; }

		public DriveCommand(double meters) { Meters = meters; }
	}

	public class TurnCommand : IFollowPathCommand
	{
		public double Degrees { get; set; }

		public TurnCommand(double degrees) { Degrees = degrees; }
	}

	public class ARTagAlignment : IFollowPathCommand
	{
		public int Dictionary { get; set; }
		public double Size { get; set; }
		public int TagId { get; set; }
		public double X { get; set; }
		public double Y { get; set; }
		public double Yaw { get; set; }
		public ARTagAlignment(int dictionary, double size, int tagId, double x, double y, double yaw)
		{
			Dictionary = dictionary;
			Size = size;
			TagId = tagId;
			X = x;
			Y = y;
			Yaw = yaw;
		}
	}

	public class DelegateCommand : IFollowPathCommand
	{
		public string Argument { get; set; }

		public Func<string, Task<bool>> Delegate { get; set; }

		public DelegateCommand(Func<string, Task<bool>> del, string arg) { Delegate = del; Argument = arg; }
	}

	public class DockCommand : IFollowPathCommand
	{
		public DockCommand() { }
	}
}
 