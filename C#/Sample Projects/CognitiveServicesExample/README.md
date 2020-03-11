# Cognitive Services Example

An example skill to show how you can use Microsoft's Cognitive Services.

### The first time you run, you will need to add startup parameters in the skill runner
http://sdk.mistyrobotics.com/skill-runner/index.html

Click on the gear icon next to the skill name that shows up once the skill is deployed to the robot.  Add the key value pairs below to the Additional Skill Parameters for your auth.

* SpeechKey
* SpeechRegion
* SpeechEndpoint

* VisionKey
* VisionRegion
* VisionEndpoint

#### NOTE: That for this example skill, these auth settings are currently being stored in a readable and unencrypted file on the robot.  You can update this as needed, but hopefully this should get you going...

### The following are all optional fields for startup and can also be changed with API endpoint calls to the "Update" event (see below)

* volume 0 - 100
* defaultvoice = default is "en-AU-HayleyRUS"
* fromdefaultlanguage = default "en-US"
* todefaultlanguage = default "en"
* foreignvoice = default is "es-MX-Raul-Apollo"
* fromforeignlanguage = default is "en-US"
* toforeignlanguage = default is "es-MX"
* profanitysetting = raw, removed, masked - default raw (allows cursing)

For some of these fields, the language and voice options are at 
https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/language-support

### Once running...
- Pushing Misty's Front Right bumper (from her perspective) will take a picture, display it on the screen and describe it.
- Pushing Misty's Front Left bumper will beep to tell the user to start recording, will record for 5 seconds, and then process the audio file and have Misty read back what you said using cognitive services to translate the speech into text and then to read the text with the selected voice.
- Pushing Misty's Back Left bumper will beep to tell the user to start recording, will record for 5 seconds, and then process the audio file and have Misty read back what you said in a different language using cognitive services.  It defaults to Spanish, but that can be changed using a REST event.
- Pushing Misty's Back Right bumper will turn face rec on and off.  When on, if Misty see's a face it knows, she will say hi.  If it is a face that she doesn't know, she will take a picture and describe it.

### You can also send user triggered events as REST commands to call events and change default and foreign voices and languages...

To send events via REST in Postman (or other tool)
POST
<robot_ip>/api/skills/event

and set the payload body as JSON

The options are...

```
//Describe the scene
{
 	"Skill": "ed3c8500-8d2c-44f6-835a-e74695f6a028",
 	"EventName": "Describe",
	"Payload" : {
 	}
 }

//Listen and repeat
{
 	"Skill": "ed3c8500-8d2c-44f6-835a-e74695f6a028",
 	"EventName": "Repeat",
	"Payload" : {
 	}
 }

//Update settings payload example, you can set any of the optional fields shown above, but you cannot change auth at runtime 
 {
 	"Skill": "ed3c8500-8d2c-44f6-835a-e74695f6a028",
 	"EventName": "Update",
	"Payload" : {
		"DefaultVoice": "en-US-BenjaminRUS"
 	}
 }

//Listen and translate
{
 	"Skill": "ed3c8500-8d2c-44f6-835a-e74695f6a028",
 	"EventName": "Translate",
	"Payload" : {
 	}
 }

//Text to Speech
{
 	"Skill": "ed3c8500-8d2c-44f6-835a-e74695f6a028",
 	"EventName": "Speak",
	"Payload" : {
		"Text": "What to say."
 	}
 }
```

Copyright 2020 Misty Robotics
Licensed under the Apache License, Version 2.0
http://www.apache.org/licenses/LICENSE-2.0
