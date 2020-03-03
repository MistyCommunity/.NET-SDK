using System;
using System.Collections.Generic;
using MistyRobotics.Common.Data;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;

namespace SkillLibrary
{
	/// <summary>
	/// Sample Mostly Empty Skill 
	/// Upon OnStart event, starts a loop to randomly change the LED until the skill is cancelled or times out
	/// </summary>
	public class MostlyHarmlessSkill : IMistySkill
	{
		/// <summary>
		/// Robot reference
		/// </summary>
		private IRobotMessenger _misty;

		/// <summary>
		/// Flag indicating class was disposed
		/// </summary>
		private bool _disposed = false;

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
		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("MostlyHarmlessSkill", "c394b08d-a172-4326-8757-5b9a2d843c93");

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
			_misty.SkillLogger.LogVerbose($"MostlyHarmlessSkill : OnStart called => Start a loop to change the LED every 250 ms");

			Random randomGenerator = new Random();

			//Pause for 100 ms, if the cancellation token is set during this time, exit the pause and the method
			while (_misty.Wait(100))
			{
				_misty.ChangeLED((uint)randomGenerator.Next(0, 256), (uint)randomGenerator.Next(0, 256), (uint)randomGenerator.Next(0, 256), null);

			}
		}

		/// <summary>
		/// Called when the skill is cancelled
		/// </summary>
		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			_misty.SkillLogger.LogVerbose($"MostlyHarmlessSkill : OnCancel called => Change LED to Red");
			_misty.ChangeLED(255, 0, 0, null);
		}

		/// <summary>
		/// Called when the skill times out
		/// </summary>
		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			_misty.SkillLogger.LogVerbose($"MostlyHarmlessSkill : OnTimeout called => Change LED to Purple");
			_misty.ChangeLED(148, 0, 211, null);
		}

		/// <summary>
		/// OnPause simply calls OnCancel in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Pause is not implemented by default and it simply calls OnCancel
			_misty.SkillLogger.LogVerbose($"MostlyHarmlessSkill : OnPause called");
			OnCancel(sender, parameters);
		}

		/// <summary>
		/// OnResume simply calls OnStart in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Resume is not implemented by default and it simply calls OnStart
			_misty.SkillLogger.LogVerbose($"MostlyHarmlessSkill : OnResume called");
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
			}

			// Free any unmanaged objects here.
			_disposed = true;
		}

		/// <summary>
		/// Skill Finalizer/Destructor
		/// </summary>
		~MostlyHarmlessSkill()
		{
			Dispose(false);
		}

		#endregion
	}
}