using System;
using System.Collections.Generic;
using System.Threading;
using MistyRobotics.Common.Data;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;

namespace SkillLibrary
{
	/// <summary>
	/// Skill to demonstrate using timer callbacks with your misty
	/// </summary>
	public class LookAroundSkill : IMistySkill
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
		/// Random Generator for the move head, change led, and move arm values
		/// </summary>
		private Random _randomGenerator = new Random();
		
		/// <summary>
		/// Timer object to perform callbacks at a regular interval to move the head
		/// </summary>
		private Timer _moveHeadTimer;

		/// <summary>
		/// Timer object to perform callbacks at a regular interval to move the arms
		/// </summary>
		private Timer _moveArmsTimer;

		/// <summary>
		/// Timer object to perform callbacks at a regular interval to change the LED
		/// </summary>
		private Timer _ledTimer;

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
		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("LookAroundSkill", "42de65c6-0d77-43bc-93bb-99eac0aae18e")
		{
			TimeoutInSeconds = 60 * 5  //runs for 5 minutes or until the skill is cancelled
		};

		/// <summary>
		///	This method is called by the wrapper to set your robot interface
		///	You need to save this off in the local variable commented on above as you are going use it to call the robot
		/// </summary>
		/// <param name="robotInterface"></param>
		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
			_misty.SkillLogger.LogLevel = SkillLogLevel.Verbose;
		}

		/// <summary>
		/// Called when the robot wants to start this skill
		/// </summary>
		/// <param name="parameters"></param>
		public void OnStart(object sender, IDictionary<string, object> parameters)
		{
			try
			{ 
				_moveHeadTimer = new Timer(MoveHeadCallback, null, 5000, 7000);
				_moveArmsTimer = new Timer(MoveArmCallback, null, 5000, 4000);
				_ledTimer = new Timer(ChangeLEDCallback, null, 1000, 1000);
			}
			catch (Exception ex)
			{
				_misty.SkillLogger.Log($"LookAroundSkill : OnStart: => Exception", ex);
			}
		}

		/// <summary>
		/// Called when the skill is cancelled
		/// </summary>
		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			_misty.SkillLogger.LogVerbose($"LookAroundSkill : OnCancel called");
			_misty.ChangeLED(255, 0, 0, null);
		}

		/// <summary>
		/// Called when the skill times out
		/// </summary>
		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			_misty.SkillLogger.LogVerbose($"LookAroundSkill : OnTimeout called");
			_misty.ChangeLED(0, 0, 255, null);
		}

		/// <summary>
		/// OnPause simply calls OnCancel in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Pause is not implemented by default and it simply calls OnCancel
			_misty.SkillLogger.LogVerbose($"LookAroundSkill : OnPause called");
			OnCancel(sender, parameters);
		}

		/// <summary>
		/// OnResume simply calls OnStart in this example
		/// </summary>
		/// <param name="parameters"></param>
		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			//In this example, Resume is not implemented by default and it simply calls OnStart
			_misty.SkillLogger.LogVerbose($"LookAroundSkill : OnResume called");
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
				_moveArmsTimer?.Dispose();
				_moveHeadTimer?.Dispose();
				_ledTimer?.Dispose();
			}

			// Free any unmanaged objects here.
			_disposed = true;
		}

		/// <summary>
		/// Skill Finalizer/Destructor
		/// </summary>
		~LookAroundSkill()
		{
			Dispose(false);
		}


		#endregion

		#region User Created Callbacks

		/// <summary>
		/// Called on the timer tick to send a random move head command to the robot
		/// </summary>
		/// <param name="info"></param>
		private void MoveHeadCallback(object info)
		{
			_misty.MoveHead(_randomGenerator.Next(-30, 15), _randomGenerator.Next(-30, 30), _randomGenerator.Next(-70, 70), _randomGenerator.Next(10, 75), AngularUnit.Degrees, null);
		}

		/// <summary>
		/// Called on the timer tick to send a random move arm command to the robot
		/// </summary>
		/// <param name="info"></param>
		private void MoveArmCallback(object info)
		{
			_misty.MoveArms(_randomGenerator.Next(-90, 90), _randomGenerator.Next(-90, 90), _randomGenerator.Next(10, 90), _randomGenerator.Next(10, 90), null, AngularUnit.Degrees, null);
		}

		/// <summary>
		/// Called on the timer tick to send a random change LED command to the robot
		/// </summary>
		/// <param name="info"></param>
		private void ChangeLEDCallback(object info)
		{
			_misty.ChangeLED((uint)_randomGenerator.Next(0, 256), (uint)_randomGenerator.Next(0, 256), (uint)_randomGenerator.Next(0, 256), null);
		}

		#endregion		
	}
}