using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aion.Commons.Configuration
{
	/// <summary>
	/// Configuration loader for .properties files with cascading override support.
	/// Matches Java PropertiesUtils behavior for parity testing.
	/// </summary>
	public class ConfigLoader
	{
		private readonly Dictionary<string, string> _properties;

		public ConfigLoader()
		{
			_properties = new Dictionary<string, string>();
		}

		/// <summary>
		/// Get a configuration value by key.
		/// </summary>
		public string? Get(string key)
		{
			return _properties.TryGetValue(key, out var value) ? value : null;
		}

		/// <summary>
		/// Get a configuration value with a default if not found.
		/// </summary>
		public string Get(string key, string defaultValue)
		{
			return _properties.TryGetValue(key, out var value) ? value : defaultValue;
		}

		/// <summary>
		/// Get value as integer with default.
		/// </summary>
		public int GetInt(string key, int defaultValue)
		{
			if (_properties.TryGetValue(key, out var value) && int.TryParse(value, out var result))
				return result;
			return defaultValue;
		}

		/// <summary>
		/// Get value as long with default.
		/// </summary>
		public long GetLong(string key, long defaultValue)
		{
			if (_properties.TryGetValue(key, out var value) && long.TryParse(value, out var result))
				return result;
			return defaultValue;
		}

		/// <summary>
		/// Get value as boolean with default.
		/// </summary>
		public bool GetBool(string key, bool defaultValue)
		{
			if (_properties.TryGetValue(key, out var value) && bool.TryParse(value, out var result))
				return result;
			return defaultValue;
		}

		/// <summary>
		/// Get value as float with default.
		/// </summary>
		public float GetFloat(string key, float defaultValue)
		{
			if (_properties.TryGetValue(key, out var value) && float.TryParse(value, out var result))
				return result;
			return defaultValue;
		}

		/// <summary>
		/// Load properties from a single file.
		/// Returns true if file was loaded, false if file not found.
		/// </summary>
		public bool LoadFromFile(string filePath)
		{
			if (!File.Exists(filePath))
				return false;

			try
			{
				foreach (var line in File.ReadLines(filePath))
				{
					var trimmed = line.Trim();

					// Skip empty lines and comments
					if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
						continue;

					// Parse key=value
					var eqIndex = trimmed.IndexOf('=');
					if (eqIndex <= 0)
						continue;

					var key = trimmed.Substring(0, eqIndex).Trim();
					var value = trimmed.Substring(eqIndex + 1).Trim();

					_properties[key] = value;
				}
				return true;
			}
			catch (Exception ex)
			{
				throw new InvalidOperationException($"Failed to load properties from {filePath}", ex);
			}
		}

		/// <summary>
		/// Load all .properties files from a directory in alphabetical order.
		/// Optional: recursively load from subdirectories.
		/// </summary>
		public void LoadFromDirectory(string dirPath, bool recursive = false)
		{
			if (!Directory.Exists(dirPath))
				throw new DirectoryNotFoundException($"Directory not found: {dirPath}");

			var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
			var files = Directory.GetFiles(dirPath, "*.properties", searchOption).OrderBy(f => f).ToList();

			foreach (var file in files)
			{
				LoadFromFile(file);
			}
		}

		/// <summary>
		/// Load configuration with cascading precedence:
		/// 1. defaultDir (base defaults)
		/// 2. overrideDir (environment-specific overrides)
		/// 3. myfile (per-instance overrides, e.g., myls.properties)
		///
		/// Later loads override earlier ones.
		/// </summary>
		public void LoadCascading(string defaultDir, string overrideDir, string myFile)
		{
			// Load defaults first
			if (Directory.Exists(defaultDir))
				LoadFromDirectory(defaultDir, false);

			// Load overrides (higher precedence)
			if (Directory.Exists(overrideDir))
				LoadFromDirectory(overrideDir, false);

			// Load my* file (highest precedence)
			LoadFromFile(myFile);
		}

		/// <summary>
		/// Get all loaded properties as a dictionary.
		/// </summary>
		public Dictionary<string, string> GetAll()
		{
			return new Dictionary<string, string>(_properties);
		}

		/// <summary>
		/// Get all keys matching a prefix (for filtering).
		/// </summary>
		public IEnumerable<string> GetKeysWithPrefix(string prefix)
		{
			return _properties.Keys.Where(k => k.StartsWith(prefix));
		}

		/// <summary>
		/// Clear all loaded properties.
		/// </summary>
		public void Clear()
		{
			_properties.Clear();
		}

		/// <summary>
		/// Set a property programmatically (for testing).
		/// </summary>
		public void Set(string key, string value)
		{
			_properties[key] = value;
		}

		/// <summary>
		/// Return count of loaded properties.
		/// </summary>
		public int Count => _properties.Count;
	}
}
