using CacheSwapper.Serializers;
using Microsoft.Data.Sqlite;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;

namespace CacheSwapper;

public class SqLiteCacheSwapper<T> : ICacheSwapper<T> where T : class
{
	private const string DATABASENAME = "CacheSwapper.sqLite";
	private const string TABLENAME = "CachedDictionary";
	private const string FIELDKEY = "Key";
	private const string FIELDVALUE = "Value";

	private readonly string dbFilePath;
	private readonly JsonSerializerOptions serializationOptions = CacheSerialization.GetOptionsWith(new ByteArrayJsonConverter());
	private readonly JsonSerializerOptions deserializationOptions = CacheSerialization.GetOptionsWith(new CacheDeserializerJsonConverter<T>());

	public IFileManager DbFileManager { get; set; }

	private string SqliteConnectionString => $"Data Source={dbFilePath}";

	public SqLiteCacheSwapper(string databasePath, IFileManager fileManager)
    {
		DbFileManager = fileManager;
		dbFilePath = DbFileManager.CombinePath(databasePath, DATABASENAME);
		InitializeDatabase();
	}

	public IEnumerable<object> Dump(Dictionary<object, T> entries, IEnumerable<object> keysToDump)
	{
		if (keysToDump.Any(k => !entries.ContainsKey(k)))
			throw new KeyNotFoundException("One or more keys are not in the entry list");

		List<object> keysToReallyDump = GetKeysNotAlreadyDumped(keysToDump);

		Task.Run(() => AddKeysToDatabase(entries, keysToReallyDump)).Wait(200);
		RemoveDumpedKeysFromEntryList(entries, keysToReallyDump);

		return keysToReallyDump;
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
		}

		return entryWasRecovered;
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
		string parameterNames = BuildParametersNamesForInsert(keysDump);
		string insertQuery = BuildInsertQuery(parameterNames);

		using (var db = new SqliteConnection(this.SqliteConnectionString))
		{
			db.Open();

			var command = new SqliteCommand(insertQuery, db);
			command.Parameters.AddRange(parameters);
			command.ExecuteNonQuery();
		}
	}

	private static string BuildInsertQuery(string parameterNames)
	{
		return new StringBuilder("INSERT INTO ").Append(TABLENAME)
			.Append(" VALUES ").Append(parameterNames).Append(';')
			.ToString();
	}

	private List<SqliteParameter> CreateParametersForInsert(Dictionary<object, T> entries, List<object> keysToReallyDump)
	{
		List<SqliteParameter> parameters = new List<SqliteParameter>();
		
		for (int i = 0; i < keysToReallyDump.Count; i++)
		{
			object key = Serialize(keysToReallyDump[i]);
			var entry = entries.Where(e => e.Key == keysToReallyDump[i]).ToDictionary(kv => kv.Key, kv => kv.Value);
			//T entry = entries[keysToReallyDump[i]];
			object? value = Serialize(entry);

			parameters.Add(new SqliteParameter($"@k_{i}", key));
			parameters.Add(new SqliteParameter($"@v_{i}", value));
		}

		return parameters;
	}

	private string BuildParametersNamesForInsert(List<object> keysToReallyDump)
	{
		var i = 0;
		return string.Join(", ", keysToReallyDump.Select(k => $"(@k_{i}, @v_{i++})"));
	}

	private List<object> GetKeysNotAlreadyDumped(IEnumerable<object> keysToDump)
	{
		var keys = keysToDump?.ToList() ?? [];
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
					if (serializedKeys.Any(sk => sk?.Equals(reader[FIELDKEY]) ?? false))
					{
						var nonSerializedKey = keysParameterized
							.First(kp => kp.Value.Value?.Equals(reader[FIELDKEY]) ?? false);
						nonExistingKeys.Remove(nonSerializedKey.Key);
					}
		}

		return nonExistingKeys;
	}

	private static string BuildSelectQueryToGetKeys(List<SqliteParameter> parameters)
	{
		string paramValues = ExtractParameterValuesForQuerySelect(parameters);

		return new StringBuilder("SELECT ").Append(FIELDKEY)
			.Append(" FROM ").Append(TABLENAME)
			.Append(" WHERE ").Append(FIELDKEY).Append(" IN (").Append(paramValues).Append(");")
			.ToString();
	}

	private static string ExtractParameterValuesForQuerySelect(List<SqliteParameter> parameters)
	{
		var parameterNames = parameters.Select(p => $"'{p.Value}'");
		return string.Join(",", parameterNames);
	}

	private Dictionary<object, SqliteParameter> CreateParametersForSelect(List<object> keys)
	{
		Dictionary<object, SqliteParameter> keysParameterized = [];
		var totalKeys = keys.Count;

		for (int i = 0; i < totalKeys; i++)
		{
			object key = Serialize(keys[i]);
			keysParameterized.Add(keys[i], new SqliteParameter($"@k{i}", key));
		}
		return keysParameterized;
	}

	private object Serialize(object value) => JsonSerializer.Serialize(value, this.serializationOptions);

	private Dictionary<object, T>? Deserialize(string jsonString) => JsonSerializer.Deserialize<Dictionary<object, T>>(jsonString, this.deserializationOptions);

	private void InitializeDatabase()
	{
		SQLitePCL.Batteries.Init();
		using (var db = new SqliteConnection(this.SqliteConnectionString))
		{
			db.Open();

			var commandText = new StringBuilder("DROP TABLE IF EXISTS ")
				.Append(TABLENAME).Append(';');

			SqliteCommand command = new SqliteCommand(commandText.ToString(), db);
			command.ExecuteReader();

			commandText = new StringBuilder("CREATE TABLE ")
				.Append(TABLENAME).Append(" (")
				.Append(FIELDKEY).Append(" TEXT NOT NULL, ")
				.Append(FIELDVALUE).Append(" TEXT NULL)");

			command = new SqliteCommand(commandText.ToString(), db);

			command.ExecuteReader();
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
		command.CommandText = BuildDeleteQueryForKey(keyParameter);
		command.ExecuteNonQuery();
	}

	private string BuildDeleteQueryForKey(SqliteParameter parameter)
	{
		return new StringBuilder("DELETE FROM ").Append(TABLENAME)
					.Append(" WHERE ").Append(FIELDKEY).Append(" = ").Append(parameter.Value).Append(';')
					.ToString();
	}

	private string BuildSelectQueryToGetKeyValue(List<SqliteParameter> parameters)
	{
		string paramValues = ExtractParameterValuesForQuerySelect(parameters);

		return new StringBuilder("SELECT [").Append(FIELDKEY).Append("], [").Append(FIELDVALUE)
			.Append("] FROM ").Append(TABLENAME)
			.Append(" WHERE ").Append(FIELDKEY).Append(" IN (").Append(paramValues).Append(");")
			.ToString();
	}
}