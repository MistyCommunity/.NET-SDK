# WanderSkill

*This example was last tested on `robotVersion 1.13.0.10362`.*

A basic C# example skill that attempts to wander an area using its bump and time-of-flight sensors.

Has two optional parameters.

- `DriveMode` - `Wander` or `Careful`. `Careful` mode attempts to stay in a small confined area and drives slower than `Wander`.  `Wander` attempts to use its time-of-flights to wander around a room. Default is `Wander`.
- `DebugMode` - `true` or `false`. If in debug mode, it will update the `LogLevel` to `Verbose`. and it will change the LED color to indicate the direction Misty should be driving. Default is `true`.

---

*Copyright 2020 Misty Robotics*<br>
*Licensed under the Apache License, Version 2.0*<br>
*http://www.apache.org/licenses/LICENSE-2.0*