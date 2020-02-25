# Drive and Dock

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
2. Use a Structure Core map for the area around the charger.
- The dock algorithm is selected with the #define statement at the top of MistySkill.cs.
- To use the map approach you will first need to create a map of the area around your charger
  and update the four MAP_* constants at the top of the MistySkills class implementation.
- Viability of the map approach will depend upon the area around your charger. With a decent
  background behind the charger the map approach works more reliably and quickly than using
  the dock detector.
- Both docking algorithms expect an alignment rail to have been installed on the charger.

The skill utilizes some helper classes within an additional project in the solution, SharedSkillTypes.
A method that wraps the Misty drive command includes a very basic obstacle avoidance system:
If Misty's hazard system stops Misty due to an obstacle she will wait for up to 30 seconds for
the obstacle to go away (or be removed) and then continue driving.
