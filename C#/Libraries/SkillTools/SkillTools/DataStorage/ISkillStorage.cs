using System.Collections.Generic;
using Windows.Foundation;

namespace SkillTools.DataStorage
{
	/// <summary>
	/// Basic data store for long terms storage of skill data
	/// Simply saves serialized dictionary data to a text file
	/// </summary>
	public interface ISkillStorage 
	{
		/// <summary>
		/// Method used to load the data from file
		/// </summary>
		/// <returns></returns>
		IAsyncOperation<IDictionary<string, object>> LoadDataAsync();

		/// <summary>
		/// Method used to store the data into a file
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		IAsyncOperation<bool> SaveDataAsync(IDictionary<string, object> data);

		/// <summary>
		/// Deletes the current database file
		/// </summary>
		/// <returns></returns>
		IAsyncOperation<bool> DeleteSkillDatabaseAsync();
	}
}