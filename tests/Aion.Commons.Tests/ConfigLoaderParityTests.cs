using System.IO;
using Aion.Commons.Configuration;

namespace Aion.Commons.Tests;

/// <summary>
/// Tests for configuration loading and override precedence.
/// Validates that .properties file loading matches Java behavior.
/// </summary>
public class ConfigLoaderParityTests : IDisposable
{
	private readonly string _testDir;

	public ConfigLoaderParityTests()
	{
		_testDir = Path.Combine(Path.GetTempPath(), $"AionConfigTest_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testDir);
	}

	public void Dispose()
	{
		if (Directory.Exists(_testDir))
			Directory.Delete(_testDir, recursive: true);
	}

	[Fact]
	public void LoadFromFile_ReadsKeyValuePairs()
	{
		var filePath = Path.Combine(_testDir, "test.properties");
		File.WriteAllText(
			filePath,
			@"
key1=value1
key2=value2
key3=123
"
		);

		var loader = new ConfigLoader();
		loader.LoadFromFile(filePath);

		Assert.Equal("value1", loader.Get("key1"));
		Assert.Equal("value2", loader.Get("key2"));
		Assert.Equal("123", loader.Get("key3"));
	}

	[Fact]
	public void LoadFromFile_IgnoresCommentsAndBlankLines()
	{
		var filePath = Path.Combine(_testDir, "test.properties");
		File.WriteAllText(
			filePath,
			@"
# This is a comment
key1=value1

# Another comment
key2=value2
"
		);

		var loader = new ConfigLoader();
		loader.LoadFromFile(filePath);

		Assert.Equal(2, loader.Count);
		Assert.Equal("value1", loader.Get("key1"));
		Assert.Equal("value2", loader.Get("key2"));
	}

	[Fact]
	public void LoadFromFile_TrimsWhitespace()
	{
		var filePath = Path.Combine(_testDir, "test.properties");
		File.WriteAllText(
			filePath,
			@"
  key1  =  value1  
  key2 = value2
"
		);

		var loader = new ConfigLoader();
		loader.LoadFromFile(filePath);

		Assert.Equal("value1", loader.Get("key1"));
		Assert.Equal("value2", loader.Get("key2"));
	}

	[Fact]
	public void LoadFromFile_ReturnsFalseIfNotFound()
	{
		var loader = new ConfigLoader();
		var result = loader.LoadFromFile("/nonexistent/path/file.properties");

		Assert.False(result);
	}

	[Fact]
	public void LoadFromDirectory_LoadsAllFilesInOrder()
	{
		var dirPath = Path.Combine(_testDir, "config");
		Directory.CreateDirectory(dirPath);

		File.WriteAllText(Path.Combine(dirPath, "a.properties"), "key=fromA\nshared=fromA");
		File.WriteAllText(Path.Combine(dirPath, "b.properties"), "key=fromB\nshared=fromB");
		File.WriteAllText(Path.Combine(dirPath, "c.properties"), "key=fromC");

		var loader = new ConfigLoader();
		loader.LoadFromDirectory(dirPath);

		// Later files override earlier ones (alphabetical order)
		Assert.Equal("fromC", loader.Get("key"));
		Assert.Equal("fromB", loader.Get("shared"));
	}

	[Fact]
	public void LoadCascading_AppliesPrecedenceCorrectly()
	{
		// Simulate Java login-server config structure
		var mainDir = Path.Combine(_testDir, "main");
		var networkDir = Path.Combine(_testDir, "network");
		var myFile = Path.Combine(_testDir, "myls.properties");

		Directory.CreateDirectory(mainDir);
		Directory.CreateDirectory(networkDir);

		// Base defaults
		File.WriteAllText(
			Path.Combine(mainDir, "defaults.properties"),
			@"
loginserver.network.client.socket_address=0.0.0.0:2106
loginserver.network.nio.threads=0
database.user=root
"
		);

		// Network overrides
		File.WriteAllText(
			Path.Combine(networkDir, "network.properties"),
			@"
loginserver.network.client.socket_address=127.0.0.1:2106
"
		);

		// Per-instance override (highest precedence)
		File.WriteAllText(
			myFile,
			@"
database.user=admin
"
		);

		var loader = new ConfigLoader();
		loader.LoadCascading(mainDir, networkDir, myFile);

		// Verify precedence
		Assert.Equal("127.0.0.1:2106", loader.Get("loginserver.network.client.socket_address")); // from network/
		Assert.Equal("admin", loader.Get("database.user")); // from myls.properties
		Assert.Equal("0", loader.Get("loginserver.network.nio.threads")); // from defaults
	}

	[Fact]
	public void GetInt_ParsesIntegerValues()
	{
		var loader = new ConfigLoader();
		loader.Set("num", "42");

		Assert.Equal(42, loader.GetInt("num", -1));
		Assert.Equal(-1, loader.GetInt("missing", -1));
	}

	[Fact]
	public void GetLong_ParsesLongValues()
	{
		var loader = new ConfigLoader();
		loader.Set("num", "123456789");

		Assert.Equal(123456789L, loader.GetLong("num", -1L));
	}

	[Fact]
	public void GetBool_ParsesBooleanValues()
	{
		var loader = new ConfigLoader();
		loader.Set("bool1", "True");
		loader.Set("bool2", "False");

		Assert.True(loader.GetBool("bool1", false));
		Assert.False(loader.GetBool("bool2", true));
	}

	[Fact]
	public void GetFloat_ParsesFloatValues()
	{
		var loader = new ConfigLoader();
		loader.Set("float", "3.14");

		Assert.Equal(3.14f, loader.GetFloat("float", 0f), 0.01f);
	}

	[Fact]
	public void GetKeysWithPrefix_FiltersCorrectly()
	{
		var loader = new ConfigLoader();
		loader.Set("loginserver.network.port", "2106");
		loader.Set("loginserver.database.url", "localhost");
		loader.Set("gameserver.port", "7777");

		var keys = loader.GetKeysWithPrefix("loginserver.").ToList();

		Assert.Equal(2, keys.Count);
		Assert.Contains("loginserver.network.port", keys);
		Assert.Contains("loginserver.database.url", keys);
	}

	[Fact]
	public void Clear_RemovesAllProperties()
	{
		var loader = new ConfigLoader();
		loader.Set("key1", "value1");
		loader.Set("key2", "value2");

		Assert.Equal(2, loader.Count);
		loader.Clear();
		Assert.Equal(0, loader.Count);
	}

	[Fact]
	public void GetAll_ReturnsAllProperties()
	{
		var loader = new ConfigLoader();
		loader.Set("key1", "value1");
		loader.Set("key2", "value2");

		var all = loader.GetAll();

		Assert.Equal(2, all.Count);
		Assert.Equal("value1", all["key1"]);
		Assert.Equal("value2", all["key2"]);
	}

	[Fact]
	public void DatabaseOptions_ParseJdbcMysqlUrl_ReadsHostPortAndDatabase()
	{
		var parsed = DatabaseOptions.ParseJdbcMysqlUrl("jdbc:mysql://db.example.test:3307/aion_ls?serverTimezone=&characterEncoding=UTF-8");

		Assert.Equal("db.example.test", parsed.Server);
		Assert.Equal(3307, parsed.Port);
		Assert.Equal("aion_ls", parsed.Database);
	}

	[Fact]
	public void DatabaseOptions_LoadFromJavaConfig_ReadsDatabaseProperties()
	{
		var options = DatabaseOptions.LoadFromJavaConfig(AppContext.BaseDirectory);

		Assert.Equal("localhost", options.Server);
		Assert.Equal(3306, options.Port);
		Assert.Equal("aion_ls", options.Database);
		Assert.Equal("root", options.UserId);
		Assert.Equal(5, options.MaxPoolSize);
		Assert.Equal(5000, options.ConnectionTimeout);
		Assert.False(string.IsNullOrEmpty(options.Password));
	}
}
