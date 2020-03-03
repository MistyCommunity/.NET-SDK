using System;
using System.Collections.Generic;
using System.Threading;
using MistyRobotics.Common.Data;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;

namespace SkillLibrary
{
	/// <summary>
	/// Another Mostly Empty Skill 
	/// Upon OnStart event, starts a timed callback to randomly change the LED until the skill is cancelled or times out
	/// </summary>
	public class MostlyHarmlessTooSkill : IMistySkill
	{		
		/// <summary>
		/// Robot reference
		/// </summary>
		private IRobotMessenger _misty;

		/// <summary>
		/// Flag indicating class was disposed
		/// </summary>
		private bool _disposed = false;

		/// <summary>
		/// Timer object to perform callbacks at a regular interval
		/// </summary>
		private Timer _heartbeatTimer;

		#region Required Skill Methods, Event Handlers & Accessors

		/// <summary>
		/// Skill details for the robot
		/// Currently you need a guid to distinguish your skill from others on the robot, get one at this link and paste it in
		/// https://www.guidgenerator.com/online-guid-generator.aspx
		/// 
		/// There are other parameters you can set if you want:
		///   Description - a description of your skill
		///   TimeoutInSeconds - timeout of skill in seconds
		///   StartupRules - a list of options to indicate if a skill should start immediately upon startup
		///   BroadcastMode - different modes can be set to share different levels of information from the robot using the 'SkillData' websocket
		///   AllowedCleanupTimeInMs - How long to wait after calling OnCancel before denying messages from the skill and performing final cleanup  
		/// </summary>
		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("MostlyHarmlessTooSkill", "82d7e714-b2b8-432a-a467-d5dfcff3d534");
	
		/// <summary>
		/// Random number generator
		/// </summary>
		private Random _randomGenerator = new Random();

		/// <summary>
		///	This method is called by the wrapper to set your robot interface
		///	You need to save this off in the local variable commented on above as you are going use it to call the robot
		/// </summary>
		/// <param name="robotInterface"></param>
		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
		}
		
		/// <summary>
		/// Called when the robot wants to start this skill
		/// </summary>
		/// <param name="parameters"></param>
		public void OnStart(object sender, IDictionary<string, object> parameters)
		{
			_misty.SkillLogger.LogVerbose($"MostlyHarmlessTooSkill : OnStart called => Start a callack to change the LED every 250 ms");
			
			//Start a timer Heartbeat callback -Periodicity Example, waits a second before starting, then changes every 250 ms
			//Will run until skill times out or is cancelled
			_heartbeatTimer = new Timer(HeartbeatCallback, null, 1000, 250);
		}

		/// <summary>
		/// Called when a user or the robot calls cancel on this skill
		/// </summary>
		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			_misty.SkillLogger.LogVerbose($"MostlyHarmlessTooSkill : OnCancel called => Change LED to Red");
			_misty.ChangeLED(255, 0, 0, null);
		}

		/// <summary>
		/// Called when the skill times out
		/// </summary>
		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			_misty.SkillLogger.LogVerbose($"MostlyHarmlessTooSkill : OnTimeout called => Change LED to Purple");
			_misty.ChangeLED(148, 0, 211, null);
		}

		/// <summary>
		/// OnPause simply calls OnCancel in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Pause is not implemented by default and it simply calls OnCancel
			_misty.SkillLogger.LogVerbose($"MostlyHarmlessTooSkill : OnPause called");
			OnCancel(sender, parameters);
		}

		/// <summary>
		/// OnResume simply calls OnStart in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Resume is not implemented by default and it simply calls OnStart
			_misty.SkillLogger.LogVerbose($"MostlyHarmlessTooSkill : OnResume called");
			OnStart(sender, parameters);
		}

		/// <summary>
		/// Dispose method must be implemented by the class
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Protected Dispose implementation
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}
			
			if (disposing)
			{
				// Free any other managed objects here.
				_heartbeatTimer?.Dispose();
			}

			// Free any unmanaged objects here.
			_disposed = true;
		}

		/// <summary>
		/// Skill Finalizer/Destructor
		/// </summary>
		~MostlyHarmlessTooSkill()
		{
			Dispose(false);
		}

		#endregion

		#region User Created Helper Methods
		
		/// <summary>
		/// Callback method for the heartbeat timer event
		/// </summary>
		/// <param name="data"></param>
		private void HeartbeatCallback(object data)
		{
			//Ignore callbacks if we are cancelling...
			if(!_misty.Wait(0)) { return; }

			_misty.ChangeLED((uint)_randomGenerator.Next(0, 256), (uint)_randomGenerator.Next(0, 256), (uint)_randomGenerator.Next(0, 256), null);
			_misty.SkillLogger.LogVerbose($"Change LED called after heartbeat callback");
		}

		#endregion
	}
}