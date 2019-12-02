using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using MistyRobotics.Common;
using MistyRobotics.SDK.Commands;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Responses;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.Common.Data;

namespace Simulator
{
	/// <summary>
	/// Example event and command processing simulator to test the skills
	/// Uses MockRobot to create a simulated environment for testing skills without a robot
	/// Needs further implementation for proper testing
	/// </summary>
	public class SimulatedMisty
	{
		public IMockRobot MockedRobot { get; }

		private IEventDetails _locomotionCommandEventDetails;
		private IEventDetails _haltCommandEventDetails;
		private IEventDetails _audioPlayCompleteCommandEventDetails;

		private bool _keyPhraseRecRunning = false;
		private bool _faceRecRunning = false;
		private bool _faceDetectRunning = false;

		//TODO Temp hack to avoid multi events
		private bool _keyPhraseRecRegistered = false;
		private bool _faceRecRegistered = false;
		private bool _capTouchRegistered = false;
		private bool _bumpRegistered = false;

		/// <summary>
		/// Starts a simulated misty to simulate command responses and events using the MockRobot
		/// </summary>
		/// <param name="skill">Instance of your skill</param>
		/// <param name="logLevel">Skill Log Level to start in</param>
		/// <param name="publishLoggerMessages">Indicates that the system should register for log messages for more information</param>
		/// <param name="loggingPreface">Overwrite default logging preface to help label runs in log</param>
		public SimulatedMisty(IMistySkill skill, SkillLogLevel logLevel = SkillLogLevel.Verbose, bool publishLoggerMessages = false, string loggingPreface = null)
		{
			//Get the mocked robot
			MockedRobot = new MockRobot(skill, logLevel, loggingPreface);

			MockedRobot.SetManualEvent(EventType.AudioPlayComplete);
			MockedRobot.SetManualEvent(EventType.TouchSensor);
			MockedRobot.SetManualEvent(EventType.BumpSensor);
			MockedRobot.SetManualEvent(EventType.KeyPhraseRecognized);
			MockedRobot.SetManualEvent(EventType.FaceRecognition);

			//Setup command listener
			MockedRobot.RobotCommandSent += HandleRobotCommand;

			//Setup Event Listeners
			MockedRobot.EventRegistered += HandleEventRegistered;

			//Prepare callbacks and events
			PrepareCommandMockedResponses();
			PrepareActuatorEventResponses();
			PrepareTOFEventResponses();

			if (publishLoggerMessages)
			{
				((IMockLogger)MockedRobot.SkillLogger).SDKLogMessageTriggered += PrintMessage;
			}
		}

		/// <summary>
		/// Write all the messages from the SDK to the debug console so we can monitor actions
		/// </summary>
		/// <param name="message"></param>
		/// <param name="exception"></param>
		private void PrintMessage(object sender, LogMessage message)
		{
			Debug.WriteLine(message.Message);
		}

		/// <summary>
		/// Populate the command response queue to return the data for these requests in the order added
		/// </summary>
		private void PrepareCommandMockedResponses()
		{
			//These modes will cause the command to return these items for every request
			MockedRobot.SetCommandQueueMode(MessageType.GetAudioList, QueueMode.RepeatLastUntilNew);
			MockedRobot.SetCommandQueueMode(MessageType.GetImageList, QueueMode.RepeatLastUntilNew);

			MockedRobot.SetNextResponse(MessageType.GetAudioList, new GetAudioListResponse(new List<AudioDetails> { new AudioDetails { Name = "Test.wav", SystemAsset = false } }));
			MockedRobot.SetNextResponse(MessageType.GetImageList, new GetImageListResponse(new List<ImageDetails> { new ImageDetails { Name = "Test.jpg", SystemAsset = false }, new ImageDetails { Name = "Test2.jpg", SystemAsset = false } }));
		}

		/// <summary>
		/// Populate the event response queue with fake Actuator events
		/// </summary>
		private void PrepareActuatorEventResponses()
		{

			//This will set the queue to send the next command in the queue until it gets to the last one.  
			//If there is only one in the queue, it will repeatedly send it until more items are added
			MockedRobot.SetEventQueueMode(EventType.ActuatorPosition, QueueMode.RepeatLastUntilNew);
			
			MockedRobot.SetNextEvent(EventType.ActuatorPosition, new MockEvent { RobotEvent = ActuatorEvent.GetMockEvent(30, ActuatorPosition.HeadPitch, -1) });
			MockedRobot.SetNextEvent(EventType.ActuatorPosition, new MockEvent { RobotEvent = ActuatorEvent.GetMockEvent(45, ActuatorPosition.HeadPitch, -1) });
			MockedRobot.SetNextEvent(EventType.ActuatorPosition, new MockEvent { RobotEvent = ActuatorEvent.GetMockEvent(20, ActuatorPosition.HeadYaw, -1) });
			MockedRobot.SetNextEvent(EventType.ActuatorPosition, new MockEvent { RobotEvent = ActuatorEvent.GetMockEvent(10, ActuatorPosition.HeadPitch, -1) });
			MockedRobot.SetNextEvent(EventType.ActuatorPosition, new MockEvent { RobotEvent = ActuatorEvent.GetMockEvent(30, ActuatorPosition.HeadPitch, -1) });
		}

		/// <summary>
		/// Populate the event response queue with fake TimeOfFlight events
		/// </summary>
		private void PrepareTOFEventResponses()
		{
			//This will set the queue to repeatedly cycle through the current events for Time Of Flight types
			MockedRobot.SetEventQueueMode(EventType.TimeOfFlight, QueueMode.Cycle);

			MockedRobot.SetNextEvent(EventType.TimeOfFlight, new MockEvent { RobotEvent = TimeOfFlightEvent.GetMockEvent(TimeOfFlightPosition.FrontCenter, .6, TimeOfFlightType.Range, 0, -1) });
			MockedRobot.SetNextEvent(EventType.TimeOfFlight, new MockEvent { RobotEvent = TimeOfFlightEvent.GetMockEvent(TimeOfFlightPosition.FrontCenter, .5, TimeOfFlightType.Range, 0, -1) });
			MockedRobot.SetNextEvent(EventType.TimeOfFlight, new MockEvent { RobotEvent = TimeOfFlightEvent.GetMockEvent(TimeOfFlightPosition.FrontCenter, .4, TimeOfFlightType.Range, 0, -1) });
			MockedRobot.SetNextEvent(EventType.TimeOfFlight, new MockEvent { RobotEvent = TimeOfFlightEvent.GetMockEvent(TimeOfFlightPosition.FrontCenter, .3, TimeOfFlightType.Range, 0, -1) });
			MockedRobot.SetNextEvent(EventType.TimeOfFlight, new MockEvent { RobotEvent = TimeOfFlightEvent.GetMockEvent(TimeOfFlightPosition.FrontCenter, .2, TimeOfFlightType.Range, 0, -1) });
			MockedRobot.SetNextEvent(EventType.TimeOfFlight, new MockEvent { RobotEvent = TimeOfFlightEvent.GetMockEvent(TimeOfFlightPosition.FrontCenter, .3, TimeOfFlightType.Range, 0, -1) });
		}


		/// <summary>
		/// Callback when an event is registered so we can setup simulated events
		/// Under development
		/// </summary>
		/// <param name="eventDetails"></param>
		/// <param name="command"></param>
		private void HandleEventRegistered(object sender, RegisterEventDetails details)
		{
			//TODO Use the event details to multi real register with specific filters
			var eventDetails = details.EventDetails;

			switch (eventDetails.EventType)
			{
				//Based on the event that was registered, then setup an event to be triggered when the commands come in
				case EventType.LocomotionCommand:
					_locomotionCommandEventDetails = eventDetails;
					break;
				case EventType.HaltCommand:
					_haltCommandEventDetails = eventDetails;
					break;
				case EventType.AudioPlayComplete:
					_audioPlayCompleteCommandEventDetails = eventDetails;
					break;

				//Record event id we registered under for manually managed events so we can send events under that id
				case EventType.TouchSensor:
					if (_capTouchRegistered) return;
					_capTouchRegistered = true;
					//Startup Cap Touch events...
					_ = Task.Run(async () =>
					{
						await Task.Delay(8000);
						MockedRobot.TriggerRobotEvent(CapTouchEvent.GetMockEvent(true, CapTouchPosition.Chin, eventDetails.EventId));
						await Task.Delay(1000);
						MockedRobot.TriggerRobotEvent(CapTouchEvent.GetMockEvent(false, CapTouchPosition.Chin, eventDetails.EventId));

						await Task.Delay(3000);
						MockedRobot.TriggerRobotEvent(CapTouchEvent.GetMockEvent(true, CapTouchPosition.Chin, eventDetails.EventId));
						await Task.Delay(1000);
						MockedRobot.TriggerRobotEvent(CapTouchEvent.GetMockEvent(false, CapTouchPosition.Chin, eventDetails.EventId));
					});
					break;
				case EventType.KeyPhraseRecognized:
					if (_keyPhraseRecRegistered) return;
					_keyPhraseRecRegistered = true;
					_ = Task.Run(async () =>
					{
						await Task.Delay(5000);
						if (_keyPhraseRecRunning)
						{
							MockedRobot.TriggerRobotEvent(new KeyPhraseRecognizedEvent(50, eventDetails.EventId));
						}
						await Task.Delay(7000);
						if (_keyPhraseRecRunning)
						{
							MockedRobot.TriggerRobotEvent(new KeyPhraseRecognizedEvent(70, eventDetails.EventId));
						}
						await Task.Delay(5000);
						if (_keyPhraseRecRunning)
						{
							MockedRobot.TriggerRobotEvent(new KeyPhraseRecognizedEvent(80, eventDetails.EventId));
						}
						await Task.Delay(4000);
						if (_keyPhraseRecRunning)
						{
							MockedRobot.TriggerRobotEvent(new KeyPhraseRecognizedEvent(70, eventDetails.EventId));
						}
					});
					break;
				case EventType.BumpSensor:
					if (_bumpRegistered) return;
					_bumpRegistered = true;
					_ = Task.Run(async () =>
					{
						await Task.Delay(10000);
						MockedRobot.TriggerRobotEvent(BumpSensorEvent.GetMockEvent(true, BumpSensorPosition.FrontLeft, eventDetails.EventId));
						await Task.Delay(3000);
						MockedRobot.TriggerRobotEvent(BumpSensorEvent.GetMockEvent(true, BumpSensorPosition.FrontRight, eventDetails.EventId));

						await Task.Delay(5000);
						MockedRobot.TriggerRobotEvent(BumpSensorEvent.GetMockEvent(false, BumpSensorPosition.FrontLeft, eventDetails.EventId));
						await Task.Delay(1000);
						MockedRobot.TriggerRobotEvent(BumpSensorEvent.GetMockEvent(false, BumpSensorPosition.FrontRight, eventDetails.EventId));
					});
					break;
				case EventType.FaceRecognition:
					if (_faceRecRegistered) return;
					_faceRecRegistered = true;
					_ = Task.Run(async () =>
					{
						await Task.Delay(5000);
						if (_faceRecRunning || _faceDetectRunning)
						{
							MockedRobot.TriggerRobotEvent(new FaceRecognitionEvent(5, 1, 1, _faceRecRunning ? "brad" : "unknown_person", 2, eventDetails.EventId));
						}
						await Task.Delay(2000);
						if (_faceRecRunning || _faceDetectRunning)
						{
							MockedRobot.TriggerRobotEvent(new FaceRecognitionEvent(5, 1, 1, _faceRecRunning ? "joe" : "unknown_person", 2, eventDetails.EventId));
						}

						await Task.Delay(4000);

						if (_faceRecRunning || _faceDetectRunning)
						{
							MockedRobot.TriggerRobotEvent(new FaceRecognitionEvent(5, 1, 1, "unknown_person", 2, eventDetails.EventId));
						}
					});
					break;
			}

		}

		/// <summary>
		/// When certain commands are trigered, we might want to do something else like trigger an event
		/// </summary>
		/// <param name="command"></param>
		/// <param name="callback"></param>
		private async void HandleRobotCommand(object sender, RobotCommandDetails details)
		{
			var command = details.RobotCommand;
			switch (command.MessageType)
			{
				//These commands trigger events if the skill is regisered
				case MessageType.Stop:
					if (_locomotionCommandEventDetails != null)
					{
						MockedRobot.TriggerRobotEvent(new LocomotionCommandEvent(0, 0, _locomotionCommandEventDetails.EventId));
					}
					break;
				case MessageType.Drive:
					if (_locomotionCommandEventDetails != null)
					{
						DriveCommand newCommand = (DriveCommand)command;
						MockedRobot.TriggerRobotEvent(new LocomotionCommandEvent(newCommand.LinearVelocity, newCommand.AngularVelocity, _locomotionCommandEventDetails.EventId));
					}
					break;
				case MessageType.DriveTime:
					if (_locomotionCommandEventDetails != null)
					{
						DriveTimeCommand newCommand = (DriveTimeCommand)command;
						MockedRobot.TriggerRobotEvent(new LocomotionCommandEvent(newCommand.LinearVelocity, newCommand.AngularVelocity, _locomotionCommandEventDetails.EventId));
					}
					break;
				case MessageType.Halt:
					if (_haltCommandEventDetails != null)
					{
						HaltCommand newCommand = (HaltCommand)command;
						MockedRobot.TriggerRobotEvent(new HaltCommandEvent(newCommand.MotorMasks, _haltCommandEventDetails.EventId));
					}
					break;
				case MessageType.PlayAudio:
					if (_audioPlayCompleteCommandEventDetails != null)
					{
						PlayAudioCommand newCommand = (PlayAudioCommand)command;
						await Task.Delay(3000);  //send an event 3 seconds after started playing for now...
						MockedRobot.TriggerRobotEvent(new AudioPlayCompleteEvent(newCommand.FileName, _audioPlayCompleteCommandEventDetails.EventId));
					}
					break;

				//Update state flag so know if events should really be sent
				case MessageType.StartKeyPhraseRecognition:
					_keyPhraseRecRunning = true;
					break;
				case MessageType.StopKeyPhraseRecognition:
					_keyPhraseRecRunning = false;
					break;
				case MessageType.StartFaceDetection:
					_faceDetectRunning = true;
					break;
				case MessageType.StartFaceRecognition:
					_faceRecRunning = true;
					break;
				case MessageType.StopFaceDetection:
					_faceDetectRunning = false;
					break;
				case MessageType.StopFaceRecognition:
					_faceRecRunning = false;
					break;
			}
		}

	}
}
