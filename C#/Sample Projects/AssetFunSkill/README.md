# AssetFun

An example skill to show how you can automatically install assets from the C# skill if they do not exist on the robot.

Has one optional startup parameter that can be passed into the skill to force it to update existing assets on the robot with the assets in the SkillAssets folder.

- ReloadAssets, defaults to false.  Setting to true will update all the assets, even if they already exist on the robot.