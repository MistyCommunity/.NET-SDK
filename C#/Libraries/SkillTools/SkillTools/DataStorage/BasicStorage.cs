using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Windows.Foundation;
using Windows.Storage;

namespace SkillTools.DataStorage
{
	/// <summary>
	/// Basic data store for long terms storage of skill data
	/// </summary>
	public abstract class BasicStorage : ISkillStorage
	{
		protected const string SkillDBFolderName = "SkillData";
		protected const string SDKFolderLocation = @"C:\Data\Misty\SDK";

		protected StorageFile _dbFile;
		protected StorageFolder _databaseFolder;
		protected SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
		protected string _fileSafeDBName;
		protected string _password;

		internal SecurityController _securityController = new SecurityController();

		/// <summary>
		/// Method used to load the data from data storage
		/// </summary>
		/// <returns></returns>
		public IAsyncOperation<IDictionary<string, object>> LoadDataAsync()
		{
			return LoadDataInternalAsync().AsAsyncOperation();
		}

		protected async Task<IDictionary<string, object>> LoadDataInternalAsync()
		{
			IDictionary<string, object> data = new Dictionary<string, object>();
			try
			{
				await _semaphoreSlim.WaitAsync();

				if (_dbFile == null)
				{
					await CreateDataStore();
				}

				if (_dbFile != null)
				{
					string dataString = File.ReadAllText(GetDbPath());

					if (string.IsNullOrWhiteSpace(dataString))
					{
						//This indicates new file or no data in existing db, grant access
						return new Dictionary<string, object>();
					}

					if (!string.IsNullOrWhiteSpace(_password))
					{
						dataString = _securityController.Decrypt(_password, dataString);
						if (dataString == null)
						{
							//This indicates bad parse with password, deny access
							return null;
						}
					}

					data = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataString, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
				}
			}
			catch
			{
				_dbFile = null;
				return null;
			}
			finally
			{
				_semaphoreSlim.Release();
			}

			return data;
		}

		/// <summary>
		/// Call to remove the current data file for this skill
		/// </summary>
		/// <returns></returns>
		public IAsyncOperation<bool> DeleteSkillDatabaseAsync()
		{
			return DeleteSkillDatabaseInternalAsync().AsAsyncOperation();
		}

		protected async Task<bool> DeleteSkillDatabaseInternalAsync()
		{
			try
			{
				await _semaphoreSlim.WaitAsync();

				if (_dbFile != null)
				{
					File.Delete(GetDbPath());
					return true;
				}
				return false;
			}
			catch
			{
				return false;
			}
			finally
			{
				_semaphoreSlim.Release();
			}
		}

		/// <summary>
		/// Method used to store the data into the long term skill data storage
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		public IAsyncOperation<bool> SaveDataAsync(IDictionary<string, object> data)
		{
			return SaveDataInternalAsync(data).AsAsyncOperation();
		}

		protected async Task<bool> SaveDataInternalAsync(IDictionary<string, object> data)
		{
			try
			{
				await _semaphoreSlim.WaitAsync();

				if (_dbFile != null && data != null && data.Count > 0)
				{
					string dataString;
					if (!string.IsNullOrWhiteSpace(_password))
					{
						dataString = _securityController.Encrypt(_password, JsonConvert.SerializeObject(data));
					}
					else
					{
						dataString = JsonConvert.SerializeObject(data);
					}

					File.WriteAllText(GetDbPath(), dataString);
					return true;
				}
				return false;
			}
			catch
			{
				_dbFile = null;
				return false;
			}
			finally
			{
				_semaphoreSlim.Release();
			}
		}

		protected string GetDbPath()
		{
			return _databaseFolder.Path + "\\" + _fileSafeDBName;
		}

		/// <summary>
		/// Attempts to create data store at C:\Data\Misty\SDK\SkillData
		/// If the path is not accessible, this is probably a robot with an older FFU
		/// So will attempt to put it in C:Data\Users\DefaultAccount\Documents\SkillData
		/// </summary>
		/// <returns></returns>
		protected async Task<bool> CreateDataStore()
		{
			if (_dbFile == null || !_dbFile.IsAvailable)
			{
				StorageFolder sdkFolder;
				try
				{
					sdkFolder = await StorageFolder.GetFolderFromPathAsync(SDKFolderLocation);
					_databaseFolder = await sdkFolder.CreateFolderAsync(SkillDBFolderName, CreationCollisionOption.OpenIfExists);
				}
				catch
				{
					try
					{
						//If old FFU build, \Misty\SDK won't exist, so save in \Users\DefaultAccount\Documents
						_databaseFolder = await KnownFolders.DocumentsLibrary.CreateFolderAsync(SkillDBFolderName, CreationCollisionOption.OpenIfExists);
					}
					catch
					{
						_dbFile = null;
						return false;
					}
				}

				try
				{
					if ((_dbFile = (StorageFile)await _databaseFolder.TryGetItemAsync(_fileSafeDBName)) == null)
					{
						_dbFile = await _databaseFolder.CreateFileAsync(_fileSafeDBName, CreationCollisionOption.ReplaceExisting);
					}
				}
				catch (Exception)
				{
					_dbFile = null;
					return false;
				}
			}

			return _dbFile != null;
		}
	}
}