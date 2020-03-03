using BasicMoqSkill;
using MistyRobotics.SDK.Messengers;
using Windows.ApplicationModel.Background;

namespace MoqExampleTask
{
    public sealed class StartupTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            RobotMessenger.LoadAndPrepareSkill(taskInstance, new MoqExampleSkill());
        }
    }
}