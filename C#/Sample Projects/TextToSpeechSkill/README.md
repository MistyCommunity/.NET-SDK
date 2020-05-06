# TextToSpeech

*This example was last tested on `robotVersion 1.13.0.10362`.*

A basic C# example skill that shows how to use text to speech and user events.  

Using the Skill Runner's advanced options for the skill, located under the skill's gear icon, you can trigger speech and display the text on the screen.
Enter in the skill event name 'Speak', and the key value pairs of 'text' and the text you wish to speak/display (no quotes for any of these fields).
Selecting Submit Event should send the event to the running skill to be processed.

You may also call it using a POST call via postman:
```  
{
 	"Skill": "32fad23d-074c-4267-8377-7fd60da380d3",
 	"EventName": "Speak",
	"Payload" : {
		"text" : "Say this, Misty!"
 	}
 }
 ```


---

*Copyright 2020 Misty Robotics*<br>
*Licensed under the Apache License, Version 2.0*<br>
*http://www.apache.org/licenses/LICENSE-2.0*
