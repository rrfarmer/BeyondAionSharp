using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;

namespace Aion.GameServer.Tests;

public sealed class StaticDataAtomicityTests
{
	[Fact]
	public async Task RequiredImportedHolder_CallbackFailureAbortsBoot()
	{
		using var fixture = new StaticDataFixture(
			"""
			<static_data>
			  <import file="events/timed_events" singleRootTag="true" />
			</static_data>
			""");
		fixture.Write(
			Path.Combine("events", "timed_events", "invalid.xml"),
			"""
			<timed_events>
			  <event name="invalid-order" start="2026-02-02T00:00:00" end="2026-02-01T00:00:00" />
			</timed_events>
			""");

		var exception = await Assert.ThrowsAsync<StaticDataHolderLoadException>(() => fixture.LoadAsync());

		Assert.Equal(typeof(EventData), exception.HolderType);
		Assert.Contains("timed_events", exception.SourcePath);
		Assert.IsType<ArgumentException>(FindRootCause(exception));
	}

	[Fact]
	public async Task OptionalExtensionFailurePreservesCurrent_AndRequiredReloadIsAtomic()
	{
		using var fixture = new StaticDataFixture("<static_data />");
		var eventFile = Path.Combine("events", "timed_events", "event.xml");
		fixture.Write(
			eventFile,
			"""
			<timed_events>
			  <event name="known-good" start="2026-01-01T00:00:00" end="2026-02-01T00:00:00" />
			</timed_events>
			""");
		var manager = await fixture.LoadAsync();
		var data = manager.StaticData;
		var knownGood = data.Events;

		Assert.Equal(StaticDataHolderRequirement.OptionalExtension, data.ClassifyHolderSource(Path.Combine(fixture.StaticDataDirectory, "events", "timed_events")));
		Assert.Equal("known-good", Assert.Single(knownGood.GetEvents()).GetName());

		fixture.Write(
			eventFile,
			"""
			<timed_events>
			  <event name="invalid-order" start="2026-02-02T00:00:00" end="2026-02-01T00:00:00" />
			</timed_events>
			""");

		data.LoadLeafHoldersFromFiles(fixture.StaticDataDirectory);
		Assert.Same(knownGood, data.Events);

		var exception = Assert.Throws<StaticDataHolderLoadException>(() => data.ReloadEventData());
		Assert.Equal(typeof(EventData), exception.HolderType);
		Assert.Same(knownGood, data.Events);
		Assert.Equal("known-good", Assert.Single(data.Events.GetEvents()).GetName());
	}

	[Fact]
	public async Task EmptyRequiredCoreHolderFailsIntegrityInvariant()
	{
		using var fixture = new StaticDataFixture(
			"""
			<static_data>
			  <import file="world_maps.xml" />
			</static_data>
			""");
		fixture.Write("world_maps.xml", "<world_maps />");

		var exception = await Assert.ThrowsAsync<StaticDataHolderLoadException>(() => fixture.LoadAsync());

		Assert.Equal(typeof(WorldMapsData), exception.HolderType);
		var rootCause = Assert.IsType<InvalidDataException>(FindRootCause(exception));
		Assert.Contains("is empty", rootCause.Message);
	}

	private static Exception FindRootCause(Exception exception)
	{
		while (exception.InnerException != null)
			exception = exception.InnerException;
		return exception;
	}

	private sealed class StaticDataFixture : IDisposable
	{
		private readonly string _root;

		public StaticDataFixture(string mainXml)
		{
			_root = Path.Combine(Path.GetTempPath(), "aion-static-atomicity-" + Guid.NewGuid().ToString("N"));
			StaticDataDirectory = Path.Combine(_root, "data", "static_data");
			Directory.CreateDirectory(StaticDataDirectory);
			File.WriteAllText(Path.Combine(StaticDataDirectory, "static_data.xml"), mainXml);
		}

		public string StaticDataDirectory { get; }

		public void Write(string relativePath, string contents)
		{
			var path = Path.Combine(StaticDataDirectory, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, contents);
		}

		public Task<DataManager> LoadAsync()
		{
			return DataManager.LoadAsync(
				new XmlDataLoaderOptions
				{
					MainXmlFilePath = Path.Combine(StaticDataDirectory, "static_data.xml"),
					CacheXmlFilePath = Path.Combine(_root, "cache", "static_data.xml"),
					SchemaFilePath = Path.Combine(StaticDataDirectory, "static_data.xsd"),
					ValidateWhenCacheChanges = false,
				});
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(_root, recursive: true);
			}
			catch
			{
			}
		}
	}
}
