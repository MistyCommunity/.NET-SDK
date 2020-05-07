# Drive and Dock

*This example was last tested on `robotVersion 1.13.0.10362`.*

The basic outline of the system is:
- A path for Misty to drive is defined within a text file located on the 410 processor.
- The path contains drive, turn, and delegate commands.
- Drive distances are in meters and turns are in degrees with counter-clockwise positive.
- The delegate command can contain anything that the developer wants to achieve.
- The path should start with Misty on her charger and end with Misty facing the charger
  from about 1 meter away.

To use the skill:
1. Put a path1.txt file on the 410 in the \\X.X.X.X\c$\Data\Users\DefaultAccount\Music\ directory.
   An example file is included in top level directory of the solution.
2. Open the DriveAndDock solution in Visual Studio.
3. Modify MistySkill.MyDelegateAsync to do something interesting.
4. Deploy the skill.
5. Run DriveAndDock from Skill Runner.

The skill can switch between two different algorithms for docking:
1. Use the dock detection functionality.
- During the docking process Misty will drive to about 2.5 meters straight back from the charger.
- The docking algorithm expects a specific dock coordinate system. This can be set by placing
  the included slam_config.json file on the 820 at /sdcard/occ/
2. Use a Structure Core map for the area around the charger.
- The dock algorithm is selected with the #define statement at the top of MistySkill.cs.
- To use the map approach you will first need to create a map of the area around your charger
  and update the four MAP_* constants at the top of the MistySkills class implementation.
- Viability of the map approach will depend upon the area around your charger. With a decent
  background behind the charger the map approach works reliably.
- Both docking algorithms expect an alignment rail to have been installed on the charger.

The skill utilizes some helper classes within an additional project in the solution, SharedSkillTypes.
A method that wraps the Misty drive command includes a very basic obstacle avoidance system:
If Misty's hazard system stops Misty due to an obstacle she will wait for up to 30 seconds for
the obstacle to go away (or be removed) and then continue driving.

Known issues:
- Hazards detected during a turn are not currently handled by the SkillHelper.TurnAsync
- There is code to verify that dock detection has been enabled with the SLAM service. However, we have
seen instances where it does not start correctly and we can't currently differentiate this failure
from a scenario where the detection is working but Misty is not facing the charger.


---

**WARRANTY DISCLAIMER.**

* General. TO THE MAXIMUM EXTENT PERMITTED BY APPLICABLE LAW, MISTY ROBOTICS PROVIDES THIS SAMPLE SOFTWARE “AS-IS” AND DISCLAIMS ALL WARRANTIES AND CONDITIONS, WHETHER EXPRESS, IMPLIED, OR STATUTORY, INCLUDING THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE, QUIET ENJOYMENT, ACCURACY, AND NON-INFRINGEMENT OF THIRD-PARTY RIGHTS. MISTY ROBOTICS DOES NOT GUARANTEE ANY SPECIFIC RESULTS FROM THE USE OF THIS SAMPLE SOFTWARE. MISTY ROBOTICS MAKES NO WARRANTY THAT THIS SAMPLE SOFTWARE WILL BE UNINTERRUPTED, FREE OF VIRUSES OR OTHER HARMFUL CODE, TIMELY, SECURE, OR ERROR-FREE.
* Use at Your Own Risk. YOU USE THIS SAMPLE SOFTWARE AND THE PRODUCT AT YOUR OWN DISCRETION AND RISK. YOU WILL BE SOLELY RESPONSIBLE FOR (AND MISTY ROBOTICS DISCLAIMS) ANY AND ALL LOSS, LIABILITY, OR DAMAGES, INCLUDING TO ANY HOME, PERSONAL ITEMS, PRODUCT, OTHER PERIPHERALS CONNECTED TO THE PRODUCT, COMPUTER, AND MOBILE DEVICE, RESULTING FROM YOUR USE OF THIS SAMPLE SOFTWARE OR PRODUCT.

Please refer to the Misty Robotics End User License Agreement for further information and full details: https://www.mistyrobotics.com/legal/end-user-license-agreement/

--- 

*Copyright 2020 Misty Robotics*<br>
*Licensed under the Apache License, Version 2.0*<br>
*http://www.apache.org/licenses/LICENSE-2.0*