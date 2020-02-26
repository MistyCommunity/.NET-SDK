using MistyRobotics.Common.Types;
using MistyRobotics.SDK.Messengers;
using Windows.ApplicationModel.Background;

namespace WanderSkill
{
    public sealed class StartupTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            RobotMessenger.LoadAndPrepareSkill
			(
				taskInstance, 
				new Wander(), 
				SkillLogLevel.Verbose
			);
        }
    }
}