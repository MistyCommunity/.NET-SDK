using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MistyRobotics.Common;
using MistyRobotics.SDK.Commands;
using MistyRobotics.Common.Data;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Responses;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;

namespace BasicMoqSkill
{
	public sealed class MoqExampleSkill : IMistySkill
	{
		private IRobotMessenger _misty;

		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("MoqExampleTask", "e60853e9-55cd-408b-9343-8cb746527289");

		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
		}

		public async void OnStart(object sender, IDictionary<string, object> parameters)
		{
			//callback example
			_misty.ChangeLED(255, 36, 0, OnResponse);
			_misty.Wait(2000);
			//second callback
			_misty.ChangeLED(255, 36, 0, OnResponse);
			_misty.Wait(1500);

			//async example
			await _misty.ChangeLEDAsync(255, 140, 0);

			//event registration
			_misty.RegisterBumpSensorEvent(BumpCallback, 0, true, null, null, null);
		}

		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			_misty.ChangeLED(0, 0, 0, OnResponse);
		}

		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			OnStart(sender, parameters);
		}

		/// <summary>
		/// This event handler is called when the cancel command is issued from the robot/user
		/// You currently have a few seconds to do cleanup and robot resets before the skill is shut down... 
		/// Events will be unregistered for you 
		/// </summary>
		public async void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			await _misty.ChangeLEDAsync(0, 0, 0);
		}

		/// <summary>
		/// This event handler is called when the skill timeouts
		/// You currently have a few seconds to do cleanup and robot resets before the skill is shut down... 
		/// Events will be unregistered for you 
		/// </summary>
		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			_misty.ChangeLED(0, 0, 0, OnResponse);
		}

		public void OnResponse(IRobotCommandResponse response)
		{
			Debug.WriteLine("Response: " + response.ResponseType.ToString());
		}

		public void BumpCallback(IBumpSensorEvent bumpEvent)
		{
			//Only processing contacted events
			if (bumpEvent.IsContacted)
			{
				switch (bumpEvent.SensorPosition)
				{
					case BumpSensorPosition.FrontRight:
						break;
					case BumpSensorPosition.FrontLeft:
						break;
					case BumpSensorPosition.BackRight:
						break;
					case BumpSensorPosition.BackLeft:
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

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~MistyNativeSkill() {
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
