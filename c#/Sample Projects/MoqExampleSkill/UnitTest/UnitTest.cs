
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Responses;
using Moq;
using MoqExampleTask;
using Windows.ApplicationModel.Background;

namespace UnitTest
{
	public class MockCommandResponse : IRobotCommandResponse
	{
		public ResponseStatus Status { get; set; }
		public string ErrorMessage { get; set; }
		public MessageType ResponseType { get; }
	}


	[TestClass]
    public class UnitTest1
    {
		[TestMethod]
        public void TestMethod1()
		{
			MoqExampleSkill moqExampleSkill = new MoqExampleSkill();
			Mock<IRobotMessenger> robotMessengerMock = new Mock<IRobotMessenger>();
			IRobotCommandResponse changeLEDResponse = new RobotCommandResponse();
			robotMessengerMock.Setup
			(
				x => x.ChangeLEDAsync
				(
					It.IsAny<uint>(), 
					It.IsAny<uint>(), 
					It.IsAny<uint>()
				)
			).Returns(Task.Factory.StartNew(() => changeLEDResponse).AsAsyncOperation());

			var test = new Action<IRobotCommandResponse>(moqExampleSkill.OnResponse);

			robotMessengerMock.Setup
			(
				x => x.ChangeLED
				(
					It.IsAny<uint>(),
					It.IsAny<uint>(),
					It.IsAny<uint>(),
					It.IsAny<ProcessCommandResponse>()
				)
			).Callback(() => moqExampleSkill.OnResponse(changeLEDResponse));
			
			moqExampleSkill.LoadRobotConnection(robotMessengerMock.Object);
			
			moqExampleSkill.OnStart(this, null);

			Task.WaitAll(Task.Delay(5000));

			moqExampleSkill.OnCancel(this, null);
		}

		private void X_RobotCommandEventReceived(object sender, MistyRobotics.SDK.Events.IRobotCommandEvent e)
		{
			throw new NotImplementedException();
		}
	}
}
