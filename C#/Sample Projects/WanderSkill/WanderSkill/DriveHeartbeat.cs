using System;
using System.Threading;

namespace WanderSkill
{
	internal class DriveHeartbeat
	{
		private DateTime _lastHeartbeatTime = DateTime.MinValue;
		
		private object _heartbeatLock = new object();

		private Timer _heartbeatTimer;
		
		public EventHandler<DateTime> HeartbeatTick { get; set; }

		public bool HeartbeatPaused { get; private set; }

		private void HeartbeatCallback(object timerData)
		{
			if(HeartbeatPaused)
			{
				return;
			}

			HeartbeatTick?.Invoke("DriveHeartbeat", _lastHeartbeatTime);
			_lastHeartbeatTime = DateTime.UtcNow;
		}

		public DriveHeartbeat(int milliseconds)
		{
			_heartbeatTimer = new Timer(HeartbeatCallback, null, milliseconds, milliseconds);
		}
		
		/// <summary>
		/// Pause the heartbeat to temporarily stop driving commands
		/// </summary>
		public void PauseHeartbeat()
		{
			lock (_heartbeatLock)
			{
				HeartbeatPaused = true;
			}
		}

		/// <summary>
		/// Continue the heartbeat to start listening to driving commands again
		/// </summary>
		public void ContinueHeartbeat()
		{
			lock (_heartbeatLock)
			{
				HeartbeatPaused = false;
			}
		}
	}
}