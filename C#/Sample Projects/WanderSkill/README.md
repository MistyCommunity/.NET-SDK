# WanderSkill

A basic C# example skill that attempts to wander an area using its bump and time of flight sensors.

Has two optional parameters.

- DriveMode, Wander or Careful.  Careful mode attempts to stay in a small confined area and drives slower than Wander.  Wander attempts to use its Time of Flights to wander around a room.  Default is Wander.
- DebugMode, true or false. If in debug mode it will update the LogLevel to Verbose and it will change the LED color to indicate the direction Misty should be driving.  Default is true.
