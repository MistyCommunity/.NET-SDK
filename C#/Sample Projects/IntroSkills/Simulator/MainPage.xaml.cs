using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Events;
using System.Collections.Generic;
using System.Threading;
using Windows.UI.Xaml.Controls;
using SkillLibrary;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Simulator
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : Page
	{
		public MainPage()
		{
			this.InitializeComponent();

			SimulatorInterface simulatorInterface = new SimulatorInterface();
		}
	}
	
	public class SimulatorInterface
	{ 

		private static SimulatedMisty _robot = null;
		private static Timer cancelTimer = null;

		public SimulatorInterface()
		{
			_robot = new SimulatedMisty(new HelloLocomotionSkill(), SkillLogLevel.Verbose, true, "MOCK DATA");

			//Optional registration example to be informed when things happen
			//eg: Could update a UI based upon these actions for a real simulator
			_robot.MockedRobot.EventRegistered += ShowEventRegistration;
			_robot.MockedRobot.EventUnregistered += ShowEventUnregistration;
			_robot.MockedRobot.EventTriggered += ShowEventTriggered;
			_robot.MockedRobot.RobotCommandSent += ShowCommandSent;
			_robot.MockedRobot.CancelSkillCommandReceived += ShowSkillCancelled;
			_robot.MockedRobot.TimeoutCommandReceived += ShowSkillTimeout;

			//Start running the skill on the Mock Robot
			_robot.MockedRobot.TriggerStartSkillEvent(new Dictionary<string, object>());

			//Simulate a cancel call in 30 seconds
			cancelTimer = new Timer(TriggerCancel, null, 30000, 0);
			
		}

		private static void TriggerCancel(object timerData)
		{
			_robot.MockedRobot.TriggerCancelSkillEvent(new Dictionary<string, object>());
		}


		private static void ShowEventRegistration(object sender, RegisterEventDetails details)
		{
			//do something
		}

		private static void ShowEventUnregistration(object sender, IEventDetails eventDetails)
		{
			//do something
		}

		private static void ShowEventTriggered(object sender, IRobotEvent robotEvent)
		{
			//do something
		}

		private static void ShowCommandSent(object sender, RobotCommandDetails details)
		{
			//do something
		}

		private static void ShowSkillCancelled(object sender, IDictionary<string, object> parameters)
		{
			//do something
		}

		private static void ShowSkillTimeout(object sender, IDictionary<string, object> parameters)
		{
			//do something
		}
	}



}
