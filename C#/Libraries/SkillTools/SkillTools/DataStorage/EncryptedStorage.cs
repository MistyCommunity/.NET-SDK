using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;

namespace SkillTools.DataStorage
{
	/// <summary>
	/// Basic data store for long terms storage of skill data
	/// IMPORTANT! These are helper data storage classes and are readable and minimally encrypted
	/// If you need better security, you may need to write your own
	/// </summary>
	public sealed class EncryptedStorage : BasicStorage
	{
		/// <summary>
		/// Method used to get a reference to the data store
		/// IMPORTANT! These are helper data storage classes and are not locked and are simply encrypted.
		/// If you need real security, you may need to write your own.
		/// Returns null if could not create file or one exists and the password is incorrect on an existing data store with data.
		/// Data is encrypted using the password, but the file is not encrypted.  The password encryption is applied when data is stored and retrieved.
		/// </summary>
		/// <param name="databaseIdentifier"></param>
		/// <param name="password"></param>
		/// <returns></returns>
		public static IAsyncOperation<ISkillStorage> GetDatabase(string databaseIdentifier, string password)
		{
			return GetDatabaseInternal(databaseIdentifier, password).AsAsyncOperation();
		}

		private static async Task<ISkillStorage> GetDatabaseInternal(string databaseIdentifier, string password)
		{
			EncryptedStorage skillDB = new EncryptedStorage(databaseIdentifier, password);

			//See if we can read the data store with that auth info
			IDictionary<string, object> existingData = await skillDB.LoadDataInternalAsync();
			if (existingData == null)
			{
				//Could not decrypt or failed to parse an existing file, don't give reference to the file...
				return null;
			}
			return skillDB;
		}

		/// <summary>
		/// IMPORTANT! These are helper data storage classes and are readable and simply encrypted.
		/// If you need real security, you may need to write your own.
		/// </summary>
		/// <param name="databaseIdentifier"></param>
		/// <param name="password"></param>
		private EncryptedStorage(string databaseIdentifier, string password)
		{
			_password = password;
			Regex invalidCharacters = new Regex(@"[\\/:*?""<>|]");
			string fileSafeSkillName = invalidCharacters.Replace(databaseIdentifier.Replace(" ", "_"), "");
			_fileSafeDBName = $"{fileSafeSkillName}.txt";
		}
	}
}