//////////////////////////////////////////////////////////////////////////////////////////
//
// This is sample prototype quality Misty skill code that represents one way to achieve certain
// functionality. Use as you wish.
//
//////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Responses;

namespace MistySkillTypes
{
	/// <summary>
	/// Steps through a collection of commands.
	/// Currently supports drive, turn, and delegate (where you can do anything you want).
	/// </summary>
	public class FollowPath
	{
		#region Private Members

		private readonly IRobotMessenger _misty;
		private readonly SkillHelper _skillHelper;

		private bool _abort;

		#endregion

		#region Public Methods

		public FollowPath(IRobotMessenger misty, SkillHelper skillHelper)
		{
			_misty = misty ?? throw new ArgumentNullException(nameof(misty));
			_skillHelper = skillHelper ?? throw new ArgumentNullException(nameof(skillHelper));
		}

		public async Task<bool> DriveAsync(List<IFollowPathCommand> commands)
		{
			_abort = false;
			bool success = true;

			foreach (IFollowPathCommand cmd in commands)
			{
				if (cmd is DriveCommand)
				{
					success = await DriveAsync(((DriveCommand)cmd).Meters);
				}
				else if (cmd is TurnCommand)
				{
					success= await TurnAsync(((TurnCommand)cmd).Degrees);
				}
				else if (cmd is DelegateCommand)
				{
					success = await((DelegateCommand)cmd).Delegate.Invoke();
				}

				if (_abort || !success) break;
			}

			return success;
		}

		public void Abort()
		{
			_abort = true;
		}

		/// <summary>
		/// Loads a sequence of Drive and Turn commands from a text file within the \Data\Users\DefaultAccount\Music\ folder
		/// </summary>
		public async Task<List<IFollowPathCommand>> LoadCommandsAsync(string filename, Func<Task<bool>> delegateCommand)
		{
			var commands = new List<IFollowPathCommand>();

			try
			{
				var folder = Windows.Storage.KnownFolders.MusicLibrary;
				var file = await folder.GetFileAsync(filename);

				var lines = await Windows.Storage.FileIO.ReadLinesAsync(file);

				foreach (string line in lines)
				{
					string[] split = line.Split(':');
					string cmd = split[0].ToUpper();
					double.TryParse(split[1], out double arg);
					switch (cmd)
					{
						case "DRIVE":
							commands.Add(new DriveCommand(arg));
							break;
						case "TURN":
							commands.Add(new TurnCommand(arg));
							break;
						case "DELEGATE":
							commands.Add(new DelegateCommand(delegateCommand));
							break;
					}
				}
			}
			catch (Exception ex)
			{
				_skillHelper.LogMessage($"Failed to read commands file, {filename}: {ex.Message}.");
			}

			return commands;
		}

		#endregion

		#region Private Methods

		private Task<bool> DriveAsync(double distance)
		{
			return _skillHelper.DriveAsync(distance);
		}

		private Task<bool> TurnAsync(double degrees)
		{
			return _skillHelper.TurnAsync(degrees);
		}

		private void OnResponse(IRobotCommandResponse commandResponse)
		{
			
		}

		#endregion
	}

	public interface IFollowPathCommand
	{

	}

	public class DriveCommand : IFollowPathCommand
	{
		public double Meters { get; set; }
		public DriveCommand(double meters) { Meters = meters;  }
	}

	public class TurnCommand : IFollowPathCommand
	{
		public double Degrees { get; set; }
		public TurnCommand(double degrees) { Degrees = degrees; }
	}

	public class DelegateCommand : IFollowPathCommand
	{
		public Func<Task<bool>> Delegate { get; set; }
		public DelegateCommand(Func<Task<bool>> del) { Delegate = del; }
	}
}
 