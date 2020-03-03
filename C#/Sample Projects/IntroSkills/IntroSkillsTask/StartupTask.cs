using SkillLibrary;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK.Messengers;
using Windows.ApplicationModel.Background;

namespace IntroSkillsTask
{
	/// <summary>
	/// Called upon deploy and when the robot attempts to connect to the skill
	/// </summary>
	public sealed class StartupTask : IBackgroundTask
	{
		/*
		 * Current intro skills in (somewhat) order of complexity:

			SkillTemplate,  //empty skill template you can use to build your skills

			MostlyHarmlessSkill,
			MostlyHarmlessTooSkill,
			HelloWorldSkill,
			HelloAgainWorldSkill,
			LookAroundSkill,
			InteractiveMistySkill,
			ForceDriving,
			HelloLocomotionSkill

			Change the skill below and re-deploy to run the skill

			If you want to run multiple at the same time, you will need to make a Skill Background Task for each skill and deploy individually - see below or documentation for more details.
			
		*/

		public void Run(IBackgroundTaskInstance taskInstance)
		{
			//Call LoadAndPrepareSkill to register robot events and add an instance of the robot interface to the skill

			RobotMessenger.LoadAndPrepareSkill
			(
				//Background task instance passed in for task management, managed by system
				taskInstance,

				//Instance of your skill
				new MostlyHarmlessSkill(),

				//Skill Log Level to start in
				SkillLogLevel.Verbose,

				//Overwrite default logging preface to help label runs in log
				"Skill Test Run #1 => "
			);

		}
	}
}


/* To create your own Background Task:
 * Create an IoT Background Task project
 * The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

 * Copy the LoadAndPrepareSkill code into the Run method and update it for your new skill name and paramters
 * 
 * Update the Package.manifest by viewing it as code and updating the following:
 *

------------------------------------------------
Update the Package XML

<Package 
	xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" 
	xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest" 
	xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" 
	xmlns:iot="http://schemas.microsoft.com/appx/manifest/iot/windows10" 
	xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3" 
	xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"  
	IgnorableNamespaces="uap uap3 mp iot rescap">

------------------------------------------------
Update the Extensions XML and replace My.Task.Namespace in the following example with the namespace of your task 

<Extensions>
	<uap3:Extension Category="windows.appExtension">
		<uap3:AppExtension Name="MistyRobotics.SDK" Id="MyTaskName" DisplayName="MyTaskDisplayName" Description="An example skill" PublicFolder="Public">
			<uap3:Properties>
				<Service>My.Task.Namespace</Service>
			</uap3:Properties>
		</uap3:AppExtension>
	</uap3:Extension>
	<Extension Category="windows.backgroundTasks" EntryPoint="My.Task.Namespace.StartupTask">
		<BackgroundTasks>
			<iot:Task Type="startup" />
	</BackgroundTasks>
	</Extension>
	<uap:Extension Category="windows.appService" EntryPoint="My.Task.Namespace.StartupTask">
		<uap:AppService Name="My.Task.Namespace" />
	</uap:Extension>
</Extensions>

------------------------------------------------
Update the Capabilities XML to allow broad file system access so the skill can write to the log... can also add other capabilities as needed here

<Capabilities>
	<Capability Name="internetClient" />
	<rescap:Capability Name="broadFileSystemAccess" />
</Capabilities>

*/
