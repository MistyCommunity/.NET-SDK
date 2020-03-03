using System;
using System.Collections.Generic;
using MistyRobotics.Common.Data;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;

namespace SkillLibrary
{
	/// <summary>
	/// Skill to demonstrate basic command sending in a loop
	/// Assumes defaults assets are on the robot, update the names of assets if they have changed
	/// </summary>
	public class HelloAgainWorldSkill : IMistySkill
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
		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("HelloAgainWorldSkill", "53b07ae8-7387-417c-bc27-9e8f4d05af7a")
		{
			TimeoutInSeconds = 60 * 1  //runs for 1 minute or until the skill is cancelled
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
		/// Loop through some head, arm, image and led actions
		/// </summary>
		/// <param name="parameters"></param>
		public void OnStart(object sender, IDictionary<string, object> parameters)
		{
			try
			{
				//These calls assume system assets on the robot at the time of writing, 
				//update as needed for new or different assets or as an exercise to allow user to pass in asset names :)

				//This loop could also be accomplished with a timed callback
				while (_misty.Wait(5000))
				{
					_misty.PlayAudio("s_Acceptance.wav", 100, null);
					_misty.MoveHead(45, 25, 0, 50, AngularUnit.Degrees, null);
					_misty.MoveArms(0, 45, 25, 60, null, AngularUnit.Degrees, null);
					_misty.ChangeLED(0, 255, 0, null);
					_misty.DisplayImage("e_Disoriented.jpg", 1, null);

					//Pause for 4 seconds, if the cancellation token is set during this time, exit the pause and the method
					if (!_misty.Wait(4000)) { return; }

					_misty.PlayAudio("s_Awe2.wav", 100, null);
					_misty.MoveHead(-10, 15, 30, 50, AngularUnit.Degrees, null);
					_misty.MoveArms(-45, 0, 60, 60, null, AngularUnit.Degrees, null);
					_misty.ChangeLED(0, 255, 255, null);
					_misty.DisplayImage("e_ContentRight.jpg", 1, null);

					//Pause for 3.5 seconds, if the cancellation token is set during this time, exit the pause and the method
					if (!_misty.Wait(3500)) { return; }

					_misty.PlayAudio("s_Joy.wav", 100, null);
					_misty.MoveHead(75, 25, 10, 50, AngularUnit.Degrees, null);
					_misty.MoveArms(-45, 45, 60, 60, null, AngularUnit.Degrees, null);
					_misty.ChangeLED(255, 255, 255, null);
					_misty.DisplayImage("e_ContentLeft.jpg", 1, null);

					//Pause for 2.5 seconds, if the cancellation token is set during this time, exit the pause and the method
					if (!_misty.Wait(2500)) { return; }

					_misty.PlayAudio("s_Joy.wav", 100, null);
					_misty.MoveHead(65, 0, -10, 50, AngularUnit.Degrees, null);
					_misty.MoveArms(0, -45, 60, 60, null, AngularUnit.Degrees, null);
					_misty.ChangeLED(0, 0, 255, null);
					_misty.DisplayImage("e_Joy.jpg", 1, null);
				}
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log($"HelloAgainWorldSkill : OnStart: => Exception", ex);
			}
		}


		/// <summary>
		/// Set the LED to red and look down on cancel
		/// </summary>
		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			_misty?.SkillLogger.LogInfo($"HelloAgainWorldSkill : OnCancel called");
			_misty.ChangeLED(255, 0, 0, null);
			_misty.MoveHead(-45, 0, 0, 50, AngularUnit.Degrees, null);
		}

		/// <summary>
		/// Turn off the led and center the head on timeout
		/// </summary>
		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			_misty?.SkillLogger.LogInfo($"HelloAgainWorldSkill : OnTimeout called");
			_misty.ChangeLED(0, 0, 0, null);
			_misty.MoveHead(0, 0, 0, 50, AngularUnit.Degrees, null);
		}

		/// <summary>
		/// OnPause simply calls OnCancel in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Pause is not implemented by default and it simply calls OnCancel
			_misty.SkillLogger.LogVerbose($"HelloAgainWorldSkill : OnPause called");
			OnCancel(sender, parameters);
		}

		/// <summary>
		/// OnResume simply calls OnStart in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Resume is not implemented by default and it simply calls OnStart
			_misty.SkillLogger.LogVerbose($"HelloAgainWorldSkill : OnResume called");
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
		~HelloAgainWorldSkill()
		{
			Dispose(false);
		}
		
		#endregion
	}
}