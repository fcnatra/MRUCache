using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System;
using MruCache.CacheSwapper.JsonConverters;

namespace MruCache.CacheSwapper;

#nullable enable
public class SqLiteCacheSwapper<T> : ICacheSwapper<T> where T : class
{
	private const string DATABASENAME = "CacheSwapper.sqLite";
	private const string TABLENAME = "CachedDictionary";
	private const string FIELDKEY = "Key";
	private const string FIELDVALUE = "Value";
	private const string SELECTOGETKEYVALUESPARAMSPLACEHOLDER = "#SELECTOGETKEYVALUESPARAMsPLACEHOLDER#";
	private const string INSERTQUERYPARAMSPLACEHOLDER = "#INSERTQUERYPARAMSPLACEHOLDER#";
	private const int SQLITE_MAX_VARIABLE_NUMBER = 999; // https://sqlite.org/limits.html

	private readonly static string selectQueryToGetKeyValueTemplate = $"SELECT [{FIELDKEY}], [{FIELDVALUE}] FROM {TABLENAME} WHERE {FIELDKEY} IN ({SELECTOGETKEYVALUESPARAMSPLACEHOLDER});";
	private readonly static string insertQueryTemplate = $"INSERT OR IGNORE INTO {TABLENAME} VALUES {INSERTQUERYPARAMSPLACEHOLDER}";
	private readonly static string selectQueryToGetNonExistingKeysTemplate = $"SELECT {FIELDKEY} FROM {TABLENAME} WHERE {FIELDKEY} IN ({SELECTOGETKEYVALUESPARAMSPLACEHOLDER});";

	private readonly string dbFilePath;
	private readonly string dbFileDirectory;
	private readonly JsonSerializerOptions serializationOptions = CacheSerialization.GetOptionsWith(new ByteArrayJsonConverter());
	private readonly JsonSerializerOptions dictionarySerializationOptions = CacheSerialization.GetOptionsWith(new DictionaryJsonConverter<T>());

	public IFileManager DbFileManager { get; set; }

	private string SqliteConnectionString => $"Data Source={dbFilePath}";
	
	public SqLiteCacheSwapper(string databasePath, IFileManager fileManager)
	{
		DbFileManager = fileManager;
		this.dbFilePath = DbFileManager.CombinePath(databasePath, DATABASENAME.Replace(".", $"{DateTime.Now.Ticks}."));
		this.dbFileDirectory = databasePath;

		InitializeDatabase();
	}

	public IEnumerable<object> Dump(Dictionary<object, T> entries, List<object> keysToDump)
	{
		if (keysToDump.Any(k => !entries.ContainsKey(k)))
			throw new KeyNotFoundException("One or more keys are not in the list of entries");

		List<object> keysToReallyDump = keysToDump.ToList(); //GetKeysNotAlreadyDumped(keysToDump); // Checking if the key is already dumped adds time to new keys - worsen performance
		var numberOfKeys = keysToReallyDump.Count();

		if (numberOfKeys <= SQLITE_MAX_VARIABLE_NUMBER)
			AddKeysToDatabase(entries, keysToReallyDump);
		else
			AddKeysToDatabaseInSmallBlocks(entries, keysToReallyDump, numberOfKeys);

		RemoveDumpedKeysFromEntryList(entries, keysToReallyDump);

		return keysToReallyDump;
	}

	private void AddKeysToDatabaseInSmallBlocks(Dictionary<object, T> entries, List<object> keysToReallyDump, int numberOfKeys)
	{
		int numberOfBlocks = numberOfKeys / SQLITE_MAX_VARIABLE_NUMBER;
		int indexStartOfBlock, numberOfKeysInThisBlock;
		for (int i = 0; i <= numberOfBlocks; i++)
		{
			indexStartOfBlock = i * SQLITE_MAX_VARIABLE_NUMBER;
			numberOfKeysInThisBlock = SQLITE_MAX_VARIABLE_NUMBER;
			if (indexStartOfBlock + numberOfKeysInThisBlock >= numberOfKeys) numberOfKeysInThisBlock = numberOfKeys - indexStartOfBlock;

			AddKeysToDatabase(entries, keysToReallyDump.GetRange(indexStartOfBlock, numberOfKeysInThisBlock));
		}
	}

	public bool Recover(Dictionary<object, T> entries, object key)
	{
		if (key is null)
			return false;

		bool entryWasRecovered = false;

		Dictionary<object, SqliteParameter> keysParameterized = CreateParametersForSelect(new List<object> { key });
		List<SqliteParameter> parameters = keysParameterized.Values.ToList();
		string commandText = BuildSelectQueryToGetKeyValue(parameters);

		SqliteParameter keyParameter = parameters.First();

		using (var db = new SqliteConnection(this.SqliteConnectionString))
		{
			db.Open();

			var command = new SqliteCommand(commandText.ToString(), db);
			command.Parameters.Add(keyParameter);

			var entriesAlreadyHadTheKey = entries.ContainsKey(key);

			if (!entriesAlreadyHadTheKey)
				entryWasRecovered = ReadEntryFromDb(entries, key, command);

			if (entriesAlreadyHadTheKey || entryWasRecovered)
				DeleteEntryFromDb(command, keyParameter);

			db.Close();
			SqliteConnection.ClearAllPools();
		}

		return entryWasRecovered;
	}

	public object[]? RecoverSeveral(Dictionary<object, T> entries, object[] keys)
	{
		if (keys == null || keys.Length == 0) { return null; }

		object[]? keysRecovered = null;

		Dictionary<object, SqliteParameter> keysParameterized = CreateParametersForSelect(keys.ToList());
		List<SqliteParameter> parameters = keysParameterized.Values.ToList();
		string commandText = BuildSelectQueryToGetKeyValue(parameters);

		using (var db = new SqliteConnection(this.SqliteConnectionString))
		{
			db.Open();

			var command = new SqliteCommand(commandText.ToString(), db);
			command.Parameters.AddRange(parameters);

			keysRecovered = ReadEntriesFromDb(entries, command);

			DeleteEntriesFromDb(command, parameters);

			db.Close();
			SqliteConnection.ClearAllPools();
		}

		return keysRecovered;
	}

	private object[] ReadEntriesFromDb(Dictionary<object, T> entries, SqliteCommand command)
	{
		var reader = command.ExecuteReader();
		object[] keysRecovered = new object[] { };

		while (reader.Read())
		{
			AddRecoveredEntryToEntryList(entries, reader[FIELDKEY], reader);
			keysRecovered.Append(reader[FIELDKEY]);
		}

		reader.Close();

		return keysRecovered;
	}

	private bool ReadEntryFromDb(Dictionary<object, T> entries, object key, SqliteCommand command)
	{
		bool wasRecovered = false;
		var reader = command.ExecuteReader();
		if (reader.Read())
		{
			wasRecovered = true;
			AddRecoveredEntryToEntryList(entries, key, reader);
			reader.Close();
		}

		return wasRecovered;
	}

	private static void RemoveDumpedKeysFromEntryList(Dictionary<object, T> entries, List<object> keysToReallyDump)
	{
		foreach (object key in keysToReallyDump)
			entries.Remove(key);
	}

	private void AddKeysToDatabase(Dictionary<object, T> entries, List<object> keysDump)
	{
		if (keysDump.Count == 0)
			return;

		List<SqliteParameter> parameters = CreateParametersForInsert(entries, keysDump);
		List<string> parameterNames = BuildParametersNamesForInsert(keysDump);
		string insertQuery = BuildInsertQuery(parameterNames);

		using (var db = new SqliteConnection(this.SqliteConnectionString))
		{
			db.Open();

			var command = new SqliteCommand(insertQuery, db);
			command.Parameters.AddRange(parameters);
			var numberOfKeysAdded = command.ExecuteNonQuery();
			Trace.WriteLineIf(numberOfKeysAdded < keysDump.Count(), "---> CACHE SWAPPER INSERTED LESS KEYS THAN REQUESTED!!!!", "WARNING");

			db.Close();
			SqliteConnection.ClearAllPools();
		}
	}

	private static string BuildInsertQuery(IEnumerable<string> parameterNames)
	{
		var queryParams = string.Join(", ", parameterNames);
		return insertQueryTemplate
			.Replace(INSERTQUERYPARAMSPLACEHOLDER, queryParams)
			.ToString();
	}

	private List<SqliteParameter> CreateParametersForInsert(Dictionary<object, T> entries, List<object> keysToDump)
	{
		List<SqliteParameter> parameters = new();

		var dictionaryWithEntryToSerialize = new Dictionary<object, T>();
		for (int i = 0; i < keysToDump.Count; i++)
		{
			dictionaryWithEntryToSerialize.Clear();
			object entryKey = keysToDump[i];
			object key = Serialize(entryKey);

			//var entry = entries.Where(e => e.Key == keysToDump[i]).ToDictionary(kv => kv.Key, kv => kv.Value);
			T entryValue = entries[entryKey];

			dictionaryWithEntryToSerialize.Add(entryKey, entryValue);
			object? value;
			try
			{
				value = SerializeDictionary(dictionaryWithEntryToSerialize);
			}
			catch (System.Exception ex)
			{
				Trace.WriteLine($"Exception {ex} - [KEY: {key} {entryKey} type: {entryKey.GetType().Name}] [VALUE: {entryValue} type: {entryValue.GetType().Name}] --- {ex.Message} on {ex.StackTrace}");
				throw;
			}
			parameters.Add(new SqliteParameter($"@k_{i}", key));
			parameters.Add(new SqliteParameter($"@v_{i}", value));
		}

		return parameters;
	}

	private List<string> BuildParametersNamesForInsert(List<object> keysToReallyDump)
	{
		var i = 0;
		return keysToReallyDump.Select(k => $"(@k_{i}, @v_{i++})").ToList();
	}

	private List<object> GetKeysNotAlreadyDumped(IEnumerable<object> keysToDump)
	{
		var keys = keysToDump?.ToList() ?? new List<object>();
		if (keys.Count == 0)
			return keys;

		Dictionary<object, SqliteParameter> keysParameterized = CreateParametersForSelect(keys);
		List<SqliteParameter> parameters = keysParameterized.Values.ToList();
		string commandText = BuildSelectQueryToGetKeys(parameters);

		var serializedKeys = parameters.Select(p => p.Value);
		List<object> nonExistingKeys = new(keys);

		using (var db = new SqliteConnection(this.SqliteConnectionString))
		{
			db.Open();

			var command = new SqliteCommand(commandText.ToString(), db);
			command.Parameters.AddRange(parameters);

			using (SqliteDataReader reader = command.ExecuteReader())
				while (reader.Read())
					if (serializedKeys.Any(sk => sk?.Equals(reader[FIELDKEY]) ?? false)) //serializedKeys.Contains(reader[FIELDKEY]))
					{
						var nonSerializedKey = keysParameterized
							.First(kp => kp.Value.Value?.Equals(reader[FIELDKEY]) ?? false);
						nonExistingKeys.Remove(nonSerializedKey.Key);
					}

			db.Close();
			SqliteConnection.ClearAllPools();
		}

		return nonExistingKeys;
	}

	private static string BuildSelectQueryToGetKeys(List<SqliteParameter> parameters)
	{
		string paramValues = ExtractParameterNamesForQuery(parameters);

		return selectQueryToGetNonExistingKeysTemplate
			.Replace(SELECTOGETKEYVALUESPARAMSPLACEHOLDER, paramValues)
			.ToString();
			//new StringBuilder("SELECT ").Append(FIELDKEY)
			//.Append(" FROM ").Append(TABLENAME)
			//.Append(" WHERE ").Append(FIELDKEY).Append(" IN (").Append(paramValues).Append(");")
			//.ToString();
	}

	private static string ExtractParameterNamesForQuery(List<SqliteParameter> parameters)
	{
		var parameterNames = parameters.Select(p => $"{p.ParameterName}");
		return string.Join(",", parameterNames);
	}

	private Dictionary<object, SqliteParameter> CreateParametersForSelect(List<object> keys)
	{
		Dictionary<object, SqliteParameter> keysParameterized = new();
		var totalKeys = keys.Count;

		for (int i = 0; i < totalKeys; i++)
		{
			object key = Serialize(keys[i]);
			keysParameterized.Add(keys[i], new SqliteParameter($"@k{i}", key));
		}
		return keysParameterized;
	}

	private object Serialize(object value) => JsonSerializer.Serialize(value, this.serializationOptions);

	private object SerializeDictionary(object value) => JsonSerializer.Serialize(value, this.dictionarySerializationOptions);

	private Dictionary<object, T>? Deserialize(string jsonString) => JsonSerializer.Deserialize<Dictionary<object, T>>(jsonString, this.dictionarySerializationOptions);

	private void InitializeDatabase()
	{
		DeleteExistingDatabaseFile();
		DbFileManager.CreateFolder(this.dbFileDirectory);
		CreateDatabase();
	}

	private void DeleteExistingDatabaseFile()
	{
		if (DbFileManager.FileExists(this.dbFilePath))
			DbFileManager.DeleteFile(dbFilePath);
	}

	private void CreateDatabase()
	{
		SQLitePCL.Batteries.Init();
		using (var db = new SqliteConnection(this.SqliteConnectionString))
		{
			db.Open();

			var commandText = new StringBuilder("DROP TABLE IF EXISTS ")
				.Append(TABLENAME).Append(';');

			SqliteCommand command = new SqliteCommand(commandText.ToString(), db);
			command.ExecuteNonQuery();

			commandText = new StringBuilder("CREATE TABLE ")
				.Append(TABLENAME).Append(" (")
				.Append(FIELDKEY).Append(" TEXT NOT NULL, ")// PRIMARY KEY, ")
				.Append(FIELDVALUE).Append(" TEXT NULL)");

			command = new SqliteCommand(commandText.ToString(), db);

			command.ExecuteNonQuery();

			db.Close();
			SqliteConnection.ClearAllPools();
		}
	}

	private void AddRecoveredEntryToEntryList(Dictionary<object, T> entries, object key, SqliteDataReader reader)
	{
		string serializedValue = (string)reader[FIELDVALUE];
		T? entryValue = null;
		if (serializedValue != null)
		{
			Dictionary<object, T>? recoveredEntries = Deserialize(serializedValue);
			entryValue = recoveredEntries?.First().Value ?? null;
		}

		if (entryValue != null)
			entries.Add(key, entryValue);
	}

	private void DeleteEntryFromDb(SqliteCommand command, SqliteParameter keyParameter)
	{
		command.CommandText = new StringBuilder("DELETE FROM ").Append(TABLENAME)
					.Append(" WHERE ")
					.Append(FIELDKEY).Append(" = ").Append(keyParameter.Value)
					.Append(';')
					.ToString();

		command.ExecuteNonQuery();
	}

	private void DeleteEntriesFromDb(SqliteCommand command, List<SqliteParameter> parameters)
	{
		command.CommandText = new StringBuilder("DELETE FROM ").Append(TABLENAME)
					.Append(" WHERE ")
					.Append(FIELDKEY).Append(" IN (")
					.Append(ExtractParameterNamesForQuery(parameters))
					.Append(");")
					.ToString();

		command.ExecuteNonQuery();
	}

	private static string BuildSelectQueryToGetKeyValue(List<SqliteParameter> parameters)
	{
		string paramValues = ExtractParameterNamesForQuery(parameters);

		return selectQueryToGetKeyValueTemplate
			.Replace(SELECTOGETKEYVALUESPARAMSPLACEHOLDER, paramValues)
			.ToString();
	}

	public void Dispose()
	{
		DeleteExistingDatabaseFile();
	}
}