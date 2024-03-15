using CacheSwapper.Serializers;
using Microsoft.Data.Sqlite;
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

		List<object> keysToReallyDump = GetNonExistingKeys(keysToDump);

		AddKeysToDatabase(entries, keysToReallyDump);
		RemoveDumpedKeysFromEntryList(entries, keysToReallyDump);

		return keysToReallyDump;
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
		string parameterNames = BuildParametersDefinitionForInsert(keysDump);
		string insertQuery = BuildInsertQuery(parameterNames);

		using (var db = new SqliteConnection(this.SqliteConnectionString))
		{
			db.Open();

			var command = new SqliteCommand(insertQuery, db);
			command.Parameters.AddRange(parameters);
			command.ExecuteNonQuery();

			db.Close();
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
			object value = Serialize(entries[keysToReallyDump[i]]);

			parameters.Add(new SqliteParameter($"@k_{i}", key));
			parameters.Add(new SqliteParameter($"@v_{i}", value));
		}

		return parameters;
	}

	private string BuildParametersDefinitionForInsert(List<object> keysToReallyDump)
	{
		var i = 0;
		return string.Join(", ", keysToReallyDump.Select(k => $"(@k_{i}, @v_{i++})"));
	}

	private List<object> GetNonExistingKeys(IEnumerable<object> keysToDump)
	{
		var keys = keysToDump?.ToList() ?? [];
		if (keys.Count == 0)
			return keys;

		Dictionary<object, SqliteParameter> keysParameterized = CreateParametersForSelect(keys);
		List<SqliteParameter> parameters = keysParameterized.Values.ToList();
		string paramValuesForQuery = ExtractParameterValuesForQuerySelect(parameters);
		string commandText = BuildSelectQuery(paramValuesForQuery);

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

			db.Close();
		}

		return nonExistingKeys;
	}

	private static string BuildSelectQuery(string paramNamesForQuery)
	{
		return new StringBuilder("SELECT ").Append(FIELDKEY)
			.Append(" FROM ").Append(TABLENAME)
			.Append(" WHERE ").Append(FIELDKEY).Append(" IN (").Append(paramNamesForQuery).Append(");")
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

	private object? Deserialize(string jsonString) => JsonSerializer.Deserialize<object>(jsonString, this.serializationOptions);

	private void InitializeDatabase()
	{
		SQLitePCL.Batteries.Init();
		using (var db = new SqliteConnection(this.SqliteConnectionString))
		{
			db.Open();

			var createTableCommand = new StringBuilder("CREATE TABLE IF NOT EXISTS ")
				.Append(TABLENAME).Append(" (")
				.Append(FIELDKEY).Append(" TEXT NOT NULL, ")
				.Append(FIELDVALUE).Append(" TEXT NULL)");

			SqliteCommand createTable = new SqliteCommand(createTableCommand.ToString(), db);

			createTable.ExecuteReader();
		}
	}

	public bool Recover(Dictionary<object, T?> entries, object key)
    {
        throw new NotImplementedException();
    }
}