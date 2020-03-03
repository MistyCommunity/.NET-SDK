# MoveArmsAndHead

A basic C# example skill that randomly moves the arms and head according to a prefined timer.

Has two optional startup parameters that can be passed into the skill to adjust the arm and head pause between movements.

- MoveHeadPause, the value in milliseconds to delay before issuing the next move head command. Must be more than 100 ms, defaults to 5000 ms.
- MoveArmPause, the value in milliseconds to delay before issuing the next move arms command.  Must be more than 100 ms, defaults to 2500 ms.

Copyright 2020 Misty Robotics
Licensed under the Apache License, Version 2.0
http://www.apache.org/licenses/LICENSE-2.0
