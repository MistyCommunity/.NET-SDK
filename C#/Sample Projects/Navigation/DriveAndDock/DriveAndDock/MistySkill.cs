//////////////////////////////////////////////////////////////////////////////////////////
//
// This is sample prototype quality Misty skill code that represents one way to achieve certain
// functionality. Use as you wish.
//
//////////////////////////////////////////////////////////////////////////////////////////

//#define MAP_DOCK

using System.Collections.Generic;
using System.Threading.Tasks;
using MistyRobotics.Common.Data;
using MistyRobotics.SDK.Responses;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;
using MistySkillTypes;

namespace DriveAndDock
{
	/// <summary>
	/// 1. Reads a file defining a path and delegate actions.
	/// 2. Misty follows the path and invokes the delegate accordingly.
	/// 3. Attempts to dock on the charger at the end of the path.
	/// See README.txt for more details.
	/// </summary>
	internal class MistySkill : IMistySkill
	{
		private const string PATH_FILE_NAME = "path1.txt";

		// Adjustments for this particular robot to achieve a level head position.
		private const double HEAD_PITCH_OFFSET = 0;
		private const double HEAD_ROLL_OFFSET = -5;
		private const double HEAD_YAW_OFFSET = -2;

		// Name of the map around the charger and the map cell values that put Misty
		// about 0.25 meters in front of the charger facing the charger.
		private const string MAP_NAME = "Map_20200128_17.46.35.UTC";
		private const int MAP_DOCK_X = 168;
		private const int MAP_DOCK_Y = 121;
		private const int MAP_DOCK_YAW = 0;

		private IRobotMessenger _misty;
		private SkillHelper _skillHelper;
		private FollowPath _followPath;
		private ChargerDock2 _docker;
		private MapNav _mapNav;

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
				_skillHelper = new SkillHelper(_misty);
				await Task.Delay(3000);

				// Load the path to follow.
				_followPath = new FollowPath(_misty, _skillHelper);
				List<IFollowPathCommand> commands = await _followPath.LoadCommandsAsync(PATH_FILE_NAME, MyDelegateAsync);

				// Follow the path
				await _followPath.DriveAsync(commands);

				// Dock
				await _skillHelper.DisableHazardSystemAsync();

				// Dock
#if MAP_DOCK
				bool started = await DockAlignAsync();
				if (started)
				{
					await _skillHelper.TurnAsync(180);
					_misty.DriveHeading(0, .6, 3000, true, OnResponse);
					await Task.Delay(4000);
					_misty.DriveHeading(0, .2, 1000, true, OnResponse);
					await Task.Delay(2000);
					_misty.PlayAudio("s_Awe.wav", 100, OnResponse);
				}
#else
				_docker = new ChargerDock2(_misty, _skillHelper, HEAD_PITCH_OFFSET, HEAD_ROLL_OFFSET, HEAD_YAW_OFFSET);
				await _docker.DockAsync();
#endif
				await _skillHelper.EnableHazardSystemAsync();

				Cleanup();
			});
		}

		private async Task<bool> MyDelegateAsync()
		{
			_skillHelper.LogMessage("Executing delegate.");

			_misty.PlayAudio("s_SystemSuccess.wav", 5, OnResponse);
			await Task.Delay(1000);

			return true;
		}

		private async Task<bool> DockAlignAsync()
		{
			_mapNav = new MapNav(_misty, _skillHelper, HEAD_PITCH_OFFSET, HEAD_ROLL_OFFSET, HEAD_YAW_OFFSET);

			bool gotPose = await _mapNav.StartTrackingAsync(MAP_NAME);
			if (!gotPose)
			{
				_misty.PlayAudio("s_Annoyance.wav", 100, OnResponse);
				await Task.Delay(2000);
				_mapNav.Cleanup();
				return false;
			}

			await _mapNav.MoveToAsync(MAP_DOCK_X, MAP_DOCK_Y, MAP_DOCK_YAW, 0);

			_mapNav.Cleanup();

			return true;
		}

		private void Cleanup()
		{
			_followPath?.Abort();
			_docker?.Abort();
			_mapNav?.Abort();
			_skillHelper?.Abort();

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
