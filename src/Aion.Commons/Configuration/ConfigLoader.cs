using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aion.Commons.Configuration
{
	/// <summary>
	/// Compatibility facade for bootstrap callers that need cascading Java properties without a static
	/// <see cref="ConfigurableProcessor"/> holder. Parsing is delegated to <see cref="JavaProperties"/> and typed
	/// values are delegated to the same transformer registry used by runtime configuration holders, so bootstrap and
	/// runtime cannot assign different meanings to the same property text.
	/// </summary>
	public class ConfigLoader
	{
		private JavaProperties _properties;

		public ConfigLoader()
		{
			_properties = new JavaProperties();
		}

		/// <summary>
		/// Get a configuration value by key.
		/// </summary>
		public string? Get(string key)
		{
			return _properties.GetProperty(key);
		}

		/// <summary>
		/// Get a configuration value with a default if not found.
		/// </summary>
		public string Get(string key, string defaultValue)
		{
			return _properties.GetProperty(key, defaultValue);
		}

		/// <summary>
		/// Get value as integer with default.
		/// </summary>
		public int GetInt(string key, int defaultValue)
		{
			return TransformOrDefault(key, defaultValue);
		}

		/// <summary>
		/// Get value as long with default.
		/// </summary>
		public long GetLong(string key, long defaultValue)
		{
			return TransformOrDefault(key, defaultValue);
		}

		/// <summary>
		/// Get value as boolean with default.
		/// </summary>
		public bool GetBool(string key, bool defaultValue)
		{
			return TransformOrDefault(key, defaultValue);
		}

		/// <summary>
		/// Get value as float with default.
		/// </summary>
		public float GetFloat(string key, float defaultValue)
		{
			return TransformOrDefault(key, defaultValue);
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
				_properties.LoadFromFile(filePath);
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

			_properties.LoadFromDirectory(dirPath, recursive);
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
			return _properties.StringPropertyNames()
				.ToDictionary(key => key, key => _properties.GetProperty(key)!, StringComparer.Ordinal);
		}

		/// <summary>
		/// Get all keys matching a prefix (for filtering).
		/// </summary>
		public IEnumerable<string> GetKeysWithPrefix(string prefix)
		{
			return _properties.StringPropertyNames().Where(k => k.StartsWith(prefix, StringComparison.Ordinal));
		}

		/// <summary>
		/// Clear all loaded properties.
		/// </summary>
		public void Clear()
		{
			_properties = new JavaProperties();
		}

		/// <summary>
		/// Set a property programmatically (for testing).
		/// </summary>
		public void Set(string key, string value)
		{
			_properties.SetProperty(key, value);
		}

		/// <summary>
		/// Return count of loaded properties.
		/// </summary>
		public int Count => _properties.Count;

		private T TransformOrDefault<T>(string key, T defaultValue)
		{
			var value = _properties.GetProperty(key);
			return value == null ? defaultValue : (T)ConfigurableProcessor.Transform(value, typeof(T))!;
		}
	}
}
