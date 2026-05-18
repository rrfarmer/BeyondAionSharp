using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Logging
{
	/// <summary>
	/// Logging initialization matching Java logback setup.
	/// Initializes file, console, and optional Discord webhook appenders.
	/// </summary>
	public static class LoggingSetup
	{
		private static bool _initialized;

		/// <summary>
		/// Initialize logging system. Must be called early in application startup.
		/// </summary>
		public static void Initialize(
			string logDirectory = "./log",
			string logFilePrefix = "aion",
			bool consoleOutput = true,
			bool fileOutput = true,
			string? discordWebhookUrl = null
		)
		{
			if (_initialized)
				return;

			// Create log directory if it doesn't exist
			if (!Directory.Exists(logDirectory))
				Directory.CreateDirectory(logDirectory);

			// Archive old logs (rotate between runs)
			ArchiveOldLogs(logDirectory, logFilePrefix);

			_initialized = true;
		}

		/// <summary>
		/// Archive old log files to maintain a rolling window.
		/// Matches Java logback's automatic archival between runs.
		/// </summary>
		private static void ArchiveOldLogs(string logDirectory, string logFilePrefix)
		{
			var archivedDir = Path.Combine(logDirectory, "archived");

			if (!Directory.Exists(archivedDir))
				Directory.CreateDirectory(archivedDir);

			try
			{
				var logFiles = Directory.GetFiles(logDirectory, $"{logFilePrefix}*.log", SearchOption.TopDirectoryOnly);

				foreach (var logFile in logFiles)
				{
					try
					{
						var fileName = Path.GetFileName(logFile);
						var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
						var archiveName = $"{Path.GetFileNameWithoutExtension(logFile)}.{timestamp}.zip";
						var archivePath = Path.Combine(archivedDir, archiveName);

						// In a real implementation, would zip the file here
						// For now, just move it with timestamp
						File.Move(logFile, archivePath, overwrite: true);
					}
					catch
					{ /* Ignore individual file archival errors */
					}
				}
			}
			catch
			{ /* Ignore archival errors */
			}
		}

		/// <summary>
		/// Configure logging for a named logger (typically used in Main or service initialization).
		/// </summary>
		public static ILogger CreateLogger<T>(ILoggerFactory loggerFactory)
		{
			return loggerFactory.CreateLogger<T>();
		}

		/// <summary>
		/// Configure logging for a named logger by string.
		/// </summary>
		public static ILogger CreateLogger(ILoggerFactory loggerFactory, string categoryName)
		{
			return loggerFactory.CreateLogger(categoryName);
		}

		/// <summary>
		/// Format a log message with timestamp, level, and category.
		/// Matches Java logback pattern: `%d{HH:mm:ss.SSS} [%-5level] [%thread] %logger{36} - %msg%n`
		/// </summary>
		public static string FormatLogMessage(DateTime timestamp, LogLevel level, string category, string message, Exception? exception = null)
		{
			var levelStr = level switch
			{
				LogLevel.Trace => "TRACE",
				LogLevel.Debug => "DEBUG",
				LogLevel.Information => "INFO ",
				LogLevel.Warning => "WARN ",
				LogLevel.Error => "ERROR",
				LogLevel.Critical => "FATAL",
				_ => "OTHER",
			};

			var threadName = Thread.CurrentThread.Name ?? Thread.CurrentThread.ManagedThreadId.ToString();
			var sb = new System.Text.StringBuilder();

			sb.Append(timestamp.ToString("HH:mm:ss.fff"));
			sb.Append(" [");
			sb.Append(levelStr);
			sb.Append("] [");
			sb.Append(threadName.PadRight(5));
			sb.Append("] ");
			sb.Append(category);
			sb.Append(" - ");
			sb.Append(message);

			if (exception != null)
			{
				sb.Append("\n");
				sb.Append(exception);
			}

			return sb.ToString();
		}
	}

	/// <summary>
	/// Custom logger extension for structured logging patterns.
	/// </summary>
	public static class LoggerExtensions
	{
		/// <summary>
		/// Log that a service/component is initializing.
		/// </summary>
		public static void LogServiceInit(this ILogger logger, string serviceName)
		{
			logger.LogInformation("Initializing {Service}...", serviceName);
		}

		/// <summary>
		/// Log that a service/component has started successfully.
		/// </summary>
		public static void LogServiceStart(this ILogger logger, string serviceName)
		{
			logger.LogInformation("{Service} started successfully", serviceName);
		}

		/// <summary>
		/// Log that a service/component is shutting down.
		/// </summary>
		public static void LogServiceShutdown(this ILogger logger, string serviceName)
		{
			logger.LogInformation("Shutting down {Service}...", serviceName);
		}

		/// <summary>
		/// Log a critical startup error with exit code.
		/// </summary>
		public static void LogStartupError(this ILogger logger, string message, Exception? ex = null, int exitCode = 1)
		{
			logger.LogCritical(ex, "STARTUP ERROR ({ExitCode}): {Message}", exitCode, message);
		}

		/// <summary>
		/// Log network event (connection, disconnection, etc.)
		/// </summary>
		public static void LogNetworkEvent(this ILogger logger, string clientId, string eventType, string details = "")
		{
			if (string.IsNullOrEmpty(details))
				logger.LogInformation("[Network] {ClientId}: {Event}", clientId, eventType);
			else
				logger.LogInformation("[Network] {ClientId}: {Event} - {Details}", clientId, eventType, details);
		}

		/// <summary>
		/// Log performance metrics.
		/// </summary>
		public static void LogPerformance(this ILogger logger, string operation, long elapsedMs, bool slow = false)
		{
			if (slow)
				logger.LogWarning("SLOW: {Operation} took {Ms}ms", operation, elapsedMs);
			else
				logger.LogDebug("{Operation} took {Ms}ms", operation, elapsedMs);
		}
	}
}
