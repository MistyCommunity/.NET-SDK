using System;
using System.Collections.Generic;
using System.Threading;
using MistyRobotics.Common.Data;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;

namespace SkillLibrary
{
	/**********************************************************************
		Copyright 2020 Misty Robotics
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