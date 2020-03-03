using System;
using System.Collections.Generic;
using MistyRobotics.Common;
using MistyRobotics.Common.Data;
using MistyRobotics.Common.Types;

namespace SkillLibrary
{
	public class RobotState
	{
		public RobotInformation DeviceInfo { get; set; }
		public CurrentLED CurrentLED { get; set; } = new CurrentLED(0, 0, 0);

		//Battery
		public double? BatteryChargePercent { get; set; }
		public double BatteryVoltage { get; set; }

		//Bumpers
		public bool LeftFrontBumper { get; set; }
		public bool RightFrontBumper { get; set; }
		public bool LeftBackBumper { get; set; }
		public bool RightBackBumper { get; set; }

		//Image Display
		public string CurrentImageDisplayed { get; set; }
		public DateTime? ImageDisplayedAt { get; set; } = null;
		public IList<ImageDetails> ImageList { get; set; } = new List<ImageDetails>();

		//Face Rec
		public string LastFaceSeen { get; set; }
		public DateTime? LastFaceSeenAt { get; set; } = null;
		
		//Velocity
		public double LinearVelocity { get; set; }
		public double AngularVelocity { get; set; }
		public double PreviousLinearVelocity { get; set; }
		public double PreviousAngularVelocity { get; set; }

		//Cap Touch
		public CapTouchPosition LastCapTouchPosition { get; set; } = CapTouchPosition.Unknown;
		public bool CapTouchIsContacted { get; set; }

		//Key Phrase
		public DateTime? LastKeyPhraseRecognizedAt { get; set; } = null;
		public bool KeyPhraseRecognitionHandled { get; set; } = true;

		//Playing Audio
		public string LastAudioPlayed { get; set; }
		public DateTime? LastAudioCompletedAt { get; set; } = null;
		public IList<AudioDetails> AudioList { get; set; } = new List<AudioDetails>();
		
		//User Data
		public IDictionary<string, object> LastUserData { get; set; } = new Dictionary<string, object>();
		public bool UserDataHandled { get; set; } = true;

		//Range TOFs
		public double FrontLeftTOF { get; set; }
		public double PreviousFrontLeftTOF { get; set; }
		public double FrontRightTOF { get; set; }
		public double PreviousFrontRightTOF { get; set; }
		//public double FrontCenterTOF { get; set; }
		public double PreviousBackTOF { get; set; }
		public double BackTOF { get; set; }

		//Edge TOFs
		public double FrontLeftEdgeTOF { get; set; }
		public double FrontRightEdgeTOF { get; set; }
		public double BackLeftEdgeTOF { get; set; }
		public double BackRightEdgeTOF { get; set; }

		//Actuators
		public double HeadPitchDegrees { get; set; }
		public double HeadRollDegrees { get; set; }
		public double HeadYawDegrees { get; set; }
		
		public double LeftArmDegrees { get; set; }
		public double RightArmDegrees { get; set; }

		//IMU

		public double PreviousRobotPitchDegrees { get; set; }
		public double PreviousRobotRollDegrees { get; set; }
		public double PreviousRobotYawDegrees { get; set; }

		public double RobotPitchDegrees { get; set; }
		public double RobotRollDegrees { get; set; }
		public double RobotYawDegrees { get; set; }
		public double RobotPitchVelocity { get; set; }
		public double RobotYawVelocity { get; set; }
		public double RobotXAcceleration { get; set; }
		public double RobotYAcceleration { get; set; }
		public double RobotZAcceleration { get; set; }

		//Drive Encoders
		public double LeftDistanceMm { get; set; }
		public double RightDistanceMm { get; set; }
		public double LeftVelocityMmPerSec { get; set; }
		public double RightVelocityMmPerSec { get; set; }

		//Audio Localization
		public int? DegreeOfArrivalSpeech { get; set; }

		public DateTime? LocalizationDataReceivedAt { get; set; } = null;
		public int[] DegreeOfArrivalNoise { get; set; }
		public int[] VoiceActivityPolar { get; set; }
		public bool[] VoiceActivitySectors { get; set; }

		public bool[] SectorsEnabled { get; set; }
		public int[] SectorStartAngles { get; set; }
		public int? GainStep { get; set; }
	}
	
}