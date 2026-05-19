using Aion.Commons.Logging;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Tests;

public sealed class AionFileLoggerProviderTests
{
	[Fact]
	public void FileLogger_WritesConsoleWarningAndErrorFiles()
	{
		var logDirectory = Path.Combine(Path.GetTempPath(), "aion-file-logger-" + Guid.NewGuid().ToString("N"));
		try
		{
			using var loggerFactory = LoggerFactory.Create(
				builder =>
				{
					builder.ClearProviders();
					builder.SetMinimumLevel(LogLevel.Trace);
					builder.AddProvider(new AionFileLoggerProvider(logDirectory));
				});
			var logger = loggerFactory.CreateLogger("Test.Category");

			logger.LogInformation("startup ok");
			logger.LogWarning("warn {Code}", 7);
			logger.LogError(new InvalidOperationException("boom"), "error {Code}", 9);

			var console = File.ReadAllText(Path.Combine(logDirectory, "server_console.log"));
			var warnings = File.ReadAllText(Path.Combine(logDirectory, "server_warnings.log"));
			var errors = File.ReadAllText(Path.Combine(logDirectory, "server_errors.log"));

			Assert.Contains("INFO", console);
			Assert.Contains("WARN", console);
			Assert.Contains("ERROR", console);
			Assert.Contains("Test.Category - startup ok", console);
			Assert.Contains("Test.Category - warn 7", warnings);
			Assert.DoesNotContain("error 9", warnings);
			Assert.Contains("Test.Category - error 9", errors);
			Assert.Contains("InvalidOperationException: boom", errors);
		}
		finally
		{
			if (Directory.Exists(logDirectory))
				Directory.Delete(logDirectory, recursive: true);
		}
	}
}
