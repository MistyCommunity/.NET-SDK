using MistyRobotics.Common.Types;
using MistyRobotics.SDK.Messengers;
using Windows.ApplicationModel.Background;

namespace CognitiveServicesExample
{
    public sealed class StartupTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
			//Load your skill and attach to the Misty robot
            RobotMessenger.LoadAndPrepareSkill
			(
				taskInstance, 
				new MistyNativeSkill(), 
				SkillLogLevel.Verbose
			);
        }
    }
}