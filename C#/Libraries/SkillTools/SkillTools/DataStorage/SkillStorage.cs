using System.Text.RegularExpressions;
using MistyRobotics.Common.Data;

namespace SkillTools.DataStorage
{
	/// <summary>
	/// Basic data store for long terms storage of skill data
	/// IMPORTANT! These are helper/example data storage classes and are readable and minimally encrypted
	/// If you need better security, you can try the EncryptedStorage class or you can need to write your own
	/// </summary>
	public sealed class SkillStorage : BasicStorage
	{
		private static SkillStorage _skillDB = null;

		private SkillStorage(INativeRobotSkill skill)
		{ 
			Regex invalidCharacters = new Regex(@"[\\/:*?""<>|]");
			string fileSafeSkillName = invalidCharacters.Replace(skill.Name.Replace(" ", "_"), "");
			_fileSafeDBName = $"{fileSafeSkillName}.txt";
		}

		/// <summary>
		/// Method used to get a reference to the skill specific data store
		/// IMPORTANT! These are helper data storage classes and this version is readable and NOT encrypted
		/// If you need security, you can try the EncryptedStorage class or you may need to write your own.
		/// </summary>
		/// <param name="skill"></param>
		/// <returns></returns>
		public static ISkillStorage GetDatabase(INativeRobotSkill skill)
		{
			if (_skillDB == null)
			{
				_skillDB = new SkillStorage(skill);
			}
			return _skillDB;
		}
	}
}
