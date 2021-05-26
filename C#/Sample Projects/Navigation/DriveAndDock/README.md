# DriveAndDock Skill

*This example was last tested on `robotVersion 1.24.10`.*

Sample code implementing a prototype quality Misty C# path following skill using dead reckoning.
This code is intended to serve as a demonstration of some Misty features. It is not a production quality,
robust implementation of an autonomous Misty.

Requires Misty software version 1.24.10 or later. This release contains improvements to the charger detection
required for this skill to run correctly.

The basic outline of the system is:
- A path for Misty to drive is defined within a text file located on the 410 processor.
- The path contains drive, turn, AR tag alignment, dock, and delegate commands.
- Drive distances are in meters and turns are in degrees with counter-clockwise positive.
- The delegate command can contain anything that the developer wants to achieve.

To use the skill:
1. Put a recipe file on to Misty's 410 processor in the c$\Data\Misty\SDK\Recipes\ directory.
   An example file, path1.txt, is included in the top level directory of the solution.
2. Open the DriveAndDock solution in Visual Studio.
3. Optionally modify MistySkill.MyDelegateAsync to do something interesting.
4. Deploy the skill to your Misty.
5. Run DriveAndDock from Skill Runner with the desired recipe file name specified as the skill parameter 'recipe'.
   By default the skill uses c$\Data\Misty\SDK\Recipes\path1.txt
   If a file name is specified as a skill parameter then the skill will look for this file within c$\Data\Misty\SDK\Recipes\

********************************************************************************************************************

SkillHelper class

A collection of generic Misty skill helper methods. Primary methods are DriveAsync and TurnAsync which wrap
Misty's built in endpoints with extra logic to help ensure that the requested drive or turn commands are successful.

********************************************************************************************************************

FollowPath class

Primarily two methods:
1. Reads each line from a recipe file and parses it into a collection of recipe steps.
2. Sequentially invokes each command in a collection of recipe steps.

Recipe step types are:
Drive:x				where x is distance in meters
Turn:x				where x is angle in degrees, positive counter-clockwise
Dock
Delegate:x			where x is an arbitrary string that is passed to your delegate method
ARTag:a,b,c,d,e,f	tag dictionary, size in mm, tag value, x in meters, y in meters, yaw in degrees. For example: ArTag:11,70,10,0.5,0.0,0.0

********************************************************************************************************************

ArTagAligner class

Encapsulates the AR tag alignment recipe command. This command enables Misty to autonomously align herself at a
specified distance and angle to an AR tag.

X is the distance of the AR tag straight ahead of Misty.
Y is the distance of the AR tag to Misty's left.
Yaw is the angle of the AR tag to Misty. 0 is perpendicular. Positive is the tag rotated counter-clockwise.

The AR tag recipe step arguments, in order, are:
Dictionary id
Tag size in mm
Tag id
X
Y
Yaw

There are three constants at the top of the class file with the tolerances for alignment. You may consider modifying
these to achieve the desired balance between precision and time spent aligning.
private const double X_TOLERANCE = 0.05;
private const double Y_TOLERANCE = 0.03;
private const double YAW_TOLERANCE = 3;

Utilizes the RegisterArTagDetectionEvent and StartArTagDetector Misty skill endpoints.

********************************************************************************************************************

ChargerDockSmall class

This class encapsulates an autonomous charger docking algorithm. It assumes that Misty is already facing 
the charger from about 0.6 meters away.

Utilizes the RegisterBatteryChargeEvent, RegisterChargerPoseEvent, RegisterSlamStatusEvent, and StartLocatingDockingStation
Misty skill endpoints.

********************************************************************************************************************

Additional Details

- The skill currently turns off the hazard system at the start of execution to avoid false positives. Turns back on at the end.

- AR tag yaw is not reliable at longer distances and/or smaller tags.
  - https://github.com/opencv/opencv/issues/8813
  - We see good results with 70mm tags up to about 0.9 meters away from Misty or 140mm tags up to about 2m.
    Longer distances may work at times, but will be less reliable.

- The skill sets Misty's head to be looking straight ahead (yaw and roll equal to 0) when aligning to the charger. If Misty's 
  head is not actually looking straight ahead during the alignment then docking may fail because the image processing will calculate
  distances and angles that won't correspond accurately to Misty's locomotion.

  If your Misty's head does not look straight ahead when yaw and roll are set to 0 then you should create a 
  c$\Data\Misty\SDK\SkillData\DockingOffsets.txt file on your 410. This file should contain up to three lines (does not need to contain
  all three):
  
  head yaw offset = 0
  head roll offset = 0
  charger y offset = 0

  The yaw and roll offset values are the values in degrees that get Misty's head to look straight ahead. When the charger docking process
  starts it uses these yaw and roll values instead of 0. For some robots a value of 0 is fine. Others have needed values between -3 and 3
  to really look straight ahead.
  
  You can obtain a pretty good yaw offset value by simply looking straight down at Misty while sending yaw movement commands from Command Center
  until Misty's head looks like its aligned straight ahead. For example, if you set Misty's head yaw to 2 degrees and this makes the head
  look straight ahead then put "head yaw offset = 2" in the DockingOffsets.txt file.

  A roll offset is less important. But if your Misty's head is clearly not level with a roll value of 0 then you should determine a roll offset
  value as well.

  Testing has shown that for some robots docking can be improved with a different charger y axis offset value. This value simply 
  shifts Misty to the left or right when she attempts to drive on to the charger. This value is best set by running the docking routine 
  a few times, observing Misty's alignment, and then adjusting this value to get Misty more centered. For example, if your Misty keeps
  ending up too far to the right side of the charger then try reducing this value from 0 to -0.01.
  
  Always test docking a few times before and after adjusting these three values as there is some variation between every run.

---

**WARRANTY DISCLAIMER.**

* General. TO THE MAXIMUM EXTENT PERMITTED BY APPLICABLE LAW, MISTY ROBOTICS PROVIDES THIS SAMPLE SOFTWARE “AS-IS” AND DISCLAIMS ALL WARRANTIES AND CONDITIONS, WHETHER EXPRESS, IMPLIED, OR STATUTORY, INCLUDING THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE, QUIET ENJOYMENT, ACCURACY, AND NON-INFRINGEMENT OF THIRD-PARTY RIGHTS. MISTY ROBOTICS DOES NOT GUARANTEE ANY SPECIFIC RESULTS FROM THE USE OF THIS SAMPLE SOFTWARE. MISTY ROBOTICS MAKES NO WARRANTY THAT THIS SAMPLE SOFTWARE WILL BE UNINTERRUPTED, FREE OF VIRUSES OR OTHER HARMFUL CODE, TIMELY, SECURE, OR ERROR-FREE.
* Use at Your Own Risk. YOU USE THIS SAMPLE SOFTWARE AND THE PRODUCT AT YOUR OWN DISCRETION AND RISK. YOU WILL BE SOLELY RESPONSIBLE FOR (AND MISTY ROBOTICS DISCLAIMS) ANY AND ALL LOSS, LIABILITY, OR DAMAGES, INCLUDING TO ANY HOME, PERSONAL ITEMS, PRODUCT, OTHER PERIPHERALS CONNECTED TO THE PRODUCT, COMPUTER, AND MOBILE DEVICE, RESULTING FROM YOUR USE OF THIS SAMPLE SOFTWARE OR PRODUCT.

Please refer to the Misty Robotics End User License Agreement for further information and full details: https://www.mistyrobotics.com/legal/end-user-license-agreement/

--- 

*Copyright 2020 Misty Robotics*<br>
*Licensed under the Apache License, Version 2.0*<br>
*http://www.apache.org/licenses/LICENSE-2.0*
