using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Logger;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Responses;
using Moq;
using BasicMoqSkill;

namespace UnitTest
{
	[TestClass]
    public class UnitTests
    {
		private int _changeLedAsyncCount = 0;
		private int _changeLedCount = 0;
		private int _bumpEventCount = 0;

		[TestMethod]
        public void TestOnStartAndOnCancel()
		{
			_changeLedAsyncCount = 0;
			_changeLedCount = 0;
			_bumpEventCount = 0;

			Mock<IRobotMessenger> robotMessengerMock = new Mock<IRobotMessenger>();			
			MoqExampleSkill moqExampleSkill = new MoqExampleSkill();

			IBumpSensorEvent bumpEvent1 = new BumpSensorEvent
			{
				EventId = 1,
				IsContacted = true,
				SensorPosition = BumpSensorPosition.FrontRight
			};

			IBumpSensorEvent bumpEvent2 = new BumpSensorEvent
			{
				EventId = 1,
				IsContacted = false,
				SensorPosition = BumpSensorPosition.FrontRight
			};

			IRobotCommandResponse successfulActionResponse = new RobotCommandResponse
			{
				ResponseType = MessageType.ActionResponse,
				Status = ResponseStatus.Success
			};
			
			//Add Wait mocks
			robotMessengerMock.SetupSequence
			(
				x => x.Wait
				(
					It.IsAny<int>()
				)
			).Returns(() =>
			{
				Task.WaitAll(Task.Delay(2000));
				return true;
			}).Returns(() =>
			{
				Task.WaitAll(Task.Delay(1500));
				return true;
			});

			//Setup mock to ensure Change LED Async has been called the appropriate amount of times
			robotMessengerMock.SetupSequence
			(
				x => x.ChangeLEDAsync
				(
					It.IsAny<uint>(), 
					It.IsAny<uint>(), 
					It.IsAny<uint>()
				)
			).Returns(Task.Factory.StartNew(() =>
			{
				_changeLedAsyncCount = 2;
				return successfulActionResponse;
			}).AsAsyncOperation())
			.Returns(Task.Factory.StartNew(() =>
			{
				_changeLedAsyncCount = 1;
				return successfulActionResponse;
			}).AsAsyncOperation());

			//Setup mock to ensure Change LED has been called the appropriate amount of times
			robotMessengerMock.Setup
			(
				x => x.ChangeLED
				(
					It.IsAny<uint>(),
					It.IsAny<uint>(),
					It.IsAny<uint>(),
					It.IsAny<ProcessCommandResponse>()
				)
			).Callback(() =>
			{
				_changeLedCount++;
				moqExampleSkill.OnResponse(successfulActionResponse);
			});

			//Setup mock to ensure bump event is registered and has a properly structured callback
			robotMessengerMock.Setup
			(
				x => x.RegisterBumpSensorEvent
				(
					It.IsAny<ProcessBumpSensorEventResponse>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<IList<BumpSensorValidation>>(), It.IsAny<string>(), null
				)
			).Callback(() =>
				{
					_bumpEventCount++;
					moqExampleSkill.BumpCallback(bumpEvent1);
					_bumpEventCount++;
					Task.WaitAll(Task.Delay(2000));
					moqExampleSkill.BumpCallback(bumpEvent2);
				}
			);

			//Load mock messenger into skill
			moqExampleSkill.LoadRobotConnection(robotMessengerMock.Object);

			//OnStart will trigger the skill's action
			moqExampleSkill.OnStart(this, null);

			//If my skill looped forever and I wanted to check data at intervals I might do this...
			//_ = Task.Run(() => moqExampleSkill.OnStart(this, null));
			//Task.WaitAll(Task.Delay(5000));

			//Test
			Assert.IsTrue(_bumpEventCount == 2);
			Assert.IsTrue(_changeLedAsyncCount == 1);
			Assert.IsTrue(_changeLedCount == 2);

			
			//Mimic cancel call
			moqExampleSkill.OnCancel(this, null);

			//test cancel actions
			Assert.IsTrue(_bumpEventCount == 2);
			Assert.IsTrue(_changeLedAsyncCount == 2);
			Assert.IsTrue(_changeLedCount == 2);
		}
	}
}
