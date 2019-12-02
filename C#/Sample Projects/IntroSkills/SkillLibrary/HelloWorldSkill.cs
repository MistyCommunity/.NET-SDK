using System;
using System.Collections.Generic;
using MistyRobotics.Common;
using MistyRobotics.Common.Data;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;

namespace SkillLibrary
{
	/// <summary>
	/// Skill to demonstrate basic robot commands
	/// Assumes defaults assets are on the robot, update the names of assets if they have changed
	/// </summary>
	public class HelloWorldSkill : IMistySkill
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
		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("HelloWorldSkill", "98ed9ccf-e84c-4b67-bd80-b6d8c1efc785")
		{
			TimeoutInSeconds = 60 * 1  //times out in 1 minute, but runs quicker than that
		};

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
			try
			{
				//These calls assume system assets on the robot at the time of writing. 
				//Update as needed for new or different assets or as an exercise to allow user to pass in asset names :)
				_misty.PlayAudio("s_Acceptance.wav", 100, null);	
				_misty.MoveHead(45, 25, 0, 50, AngularUnit.Degrees, null);
				_misty.MoveArms(0, 45, 25, 60, null, AngularUnit.Degrees, null);
				_misty.ChangeLED(0, 255, 0, null);
				_misty.DisplayImage("e_Disoriented.jpg", 1, null);

				//Pause for 4 seconds, if the cancellation token is set during this time, exit the pause and the method
				if (!_misty.Wait(4000)) { return; }

				_misty.PlayAudio("s_Awe2.wav", 100, null);
				_misty.MoveHead(75, 15, 30, 50, AngularUnit.Degrees, null);
				_misty.MoveArms(-45, 0, 60, 100, null, AngularUnit.Degrees, null);
				_misty.ChangeLED(0, 255, 255, null);
				_misty.DisplayImage("e_ContentRight.jpg", 1, null);

				//Pause for 3.5 seconds, if the cancellation token is set during this time, exit the pause and the method
				if (!_misty.Wait(3500)) { return; }

				_misty.PlayAudio("s_Joy.wav", 100, null);
				_misty.MoveHead(75, 25, 10, 50, AngularUnit.Degrees, null);
				_misty.MoveArms(-45, 45, 60, 100, null, AngularUnit.Degrees, null);
				_misty.ChangeLED(255, 255, 255, null);
				_misty.DisplayImage("e_ContentLeft.jpg", 1, null);

				//Pause for 2.5 seconds, if the cancellation token is set during this time, exit the pause and the method
				if (!_misty.Wait(2500)) { return; }

				_misty.PlayAudio("s_PhraseHello.wav", 100, null);
				_misty.MoveHead(-10, 0, -10, 50, AngularUnit.Degrees, null);
				_misty.MoveArms(0, -45, 60, 100, null, AngularUnit.Degrees, null);
				_misty.ChangeLED(0, 0, 255, null);
				_misty.DisplayImage("e_Joy.jpg", 1, null);

				//Tell the robot the skill has completed early
				_misty.SkillCompleted();  
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log($"HelloWorldSkill : OnStart: => Exception", ex);
			}
		}

		/// <summary>
		/// Called when the skill is cancelled
		/// </summary>
		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			_misty?.SkillLogger.LogInfo($"HelloWorldSkill : OnCancel called");
		}

		/// <summary>
		/// Called when the skill times out
		/// </summary>
		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			_misty?.SkillLogger.LogInfo($"HelloWorldSkill : OnTimeout called");
		}

		/// <summary>
		/// OnPause simply calls OnCancel in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Pause is not implemented by default and it simply calls OnCancel
			_misty.SkillLogger.LogVerbose($"HelloWorldSkill : OnPause called");
			OnCancel(sender, parameters);
		}

		/// <summary>
		/// OnResume simply calls OnStart in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Resume is not implemented by default and it simply calls OnStart
			_misty.SkillLogger.LogVerbose($"HelloWorldSkill : OnResume called");
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
		~HelloWorldSkill()
		{
			Dispose(false);
		}
		
		#endregion
	}
}