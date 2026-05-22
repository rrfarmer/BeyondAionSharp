using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class RiftScheduleServiceTests
{
	[Fact]
	public void RiftScheduleTable_LoadsJavaScheduleXml()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-schedule-table-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		var schedulePath = Path.Combine(tempPath, "rift_schedule.xml");
		try
		{
			File.WriteAllText(
				schedulePath,
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<rift_schedule>
					<rift id="210050000">
						<open schedule="0 0 * ? * *"/>
						<open schedule="0 0 18 ? * FRI,MON" spawn="true"/>
					</rift>
				</rift_schedule>
				""");

			var table = RiftScheduleTable.LoadFromFile(schedulePath);

			Assert.Equal(2, table.Count);
			Assert.Equal(new RiftScheduleEntry(210050000, "0 0 * ? * *", false), table.Entries[0]);
			Assert.Equal(new RiftScheduleEntry(210050000, "0 0 18 ? * FRI,MON", true), table.Entries[1]);
		}
		finally
		{
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task InitAsync_RegistersOneScheduledTaskPerOpenEntry()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-schedule-init-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		var schedulePath = Path.Combine(tempPath, "rift_schedule.xml");
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			File.WriteAllText(
				schedulePath,
				"""
				<?xml version="1.0" encoding="UTF-8"?>
				<rift_schedule>
					<rift id="210020000">
						<open schedule="0 0 * ? * *"/>
						<open schedule="0 0 18 ? * FRI,MON" spawn="true"/>
					</rift>
				</rift_schedule>
				""");
			var (_, _, riftService, informer) = await CreateRiftServicesAsync(tempPath);
			var scheduleService = new RiftScheduleService(
				riftService,
				informer,
				threadPoolManager,
				options: new GameServerOptions(),
				clock: () => new DateTimeOffset(2026, 5, 22, 17, 59, 0, TimeSpan.Zero),
				scheduleFilePath: schedulePath);

			await scheduleService.InitAsync();

			Assert.Equal(2, scheduleService.Openings.Count);
			Assert.Equal(2, scheduleService.ScheduledTaskCount);
			Assert.Contains(scheduleService.Openings, opening => opening is { WorldId: 210020000, SpawnGuards: false });
			Assert.Contains(scheduleService.Openings, opening => opening is { WorldId: 210020000, SpawnGuards: true });

			await scheduleService.ShutdownAsync();
		}
		finally
		{
			await threadPoolManager.ShutdownAsync(TimeSpan.FromMilliseconds(10));
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	[Fact]
	public async Task RunOpeningAsync_MirrorsRiftOpenRunnableSideEffects()
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "aion-rift-schedule-run-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempPath);
		var threadPoolManager = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
		try
		{
			var (world, manager, riftService, informer) = await CreateRiftServicesAsync(tempPath);
			Assert.True(JavaQuartzCronExpression.TryParse("0 0 * ? * *", out var cronExpression));
			var scheduleService = new RiftScheduleService(
				riftService,
				informer,
				threadPoolManager,
				options: new GameServerOptions
				{
					Custom = new GameServerCustomOptions
					{
						RiftDuration = 1,
					},
				});
			var opening = new RiftScheduledOpening(210020000, false, "0 0 * ? * *", cronExpression);

			await scheduleService.RunOpeningAsync(opening);

			Assert.True(riftService.IsRiftOpened(2120));
			Assert.Equal(1, riftService.ActiveRiftCount);
			Assert.Equal(2, world.ObjectCount);
			Assert.Equal(2, manager.SpawnedRiftCount);
			Assert.Equal(1, scheduleService.ScheduledTaskCount);
		}
		finally
		{
			await threadPoolManager.ShutdownAsync(TimeSpan.FromMilliseconds(10));
			try
			{
				Directory.Delete(tempPath, recursive: true);
			}
			catch
			{
			}
		}
	}

	private static async Task<(GameWorld World, RiftManagerService Manager, RiftService RiftService, RiftInformerService Informer)> CreateRiftServicesAsync(
		string tempPath)
	{
		var context = await CreateRuntimeContextAsync(tempPath);
		var idFactory = new IDFactory();
		var world = new GameWorld(NullLogger<GameWorld>.Instance);
		var manager = new RiftManagerService(context, world, idFactory);
		var riftService = new RiftService(
			context,
			manager,
			world,
			idFactory,
			nextBoolean: () => false,
			randomInclusive: (_, _) => 1);
		var informer = new RiftInformerService(riftService);
		return (world, manager, riftService, informer);
	}

	private static async Task<GameServerRuntimeContext> CreateRuntimeContextAsync(string tempPath)
	{
		var staticDataFile = Path.Combine(tempPath, "static_data.xml");
		var cacheFile = Path.Combine(tempPath, "cache", "static_data.xml");
		var schemaFile = Path.Combine(tempPath, "static_data.xsd");
		Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
		File.WriteAllText(
			staticDataFile,
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<rift_locations>
					<rift_location id="2120" world="210020000" />
				</rift_locations>
				<npc_templates>
					<npc_template npc_id="730100" name="master rift" name_id="730100" level="1" rank="NORMAL" rating="NORMAL" race="ELYOS" tribe="FIELD_OBJECT_ALL" type="GENERAL" state="5" ai="portal" />
					<npc_template npc_id="730101" name="slave rift" name_id="730101" level="1" rank="NORMAL" rating="NORMAL" race="ASMODIANS" tribe="FIELD_OBJECT_ALL" type="GENERAL" state="6" ai="portal" />
				</npc_templates>
				<spawns>
					<spawn_map map_id="210020000">
						<spawn npc_id="730100" handler="RIFT">
							<spot x="1" y="2" z="3" anchor="ELTNEN_AM" />
						</spawn>
					</spawn_map>
					<spawn_map map_id="220020000">
						<spawn npc_id="730101" handler="RIFT">
							<spot x="11" y="12" z="13" anchor="MORHEIM_AS" />
						</spawn>
					</spawn_map>
				</spawns>
			</static_data>
			""");
		File.WriteAllText(schemaFile, """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" />""");
		var dataManager = await DataManager.LoadAsync(
			new XmlDataLoaderOptions
			{
				MainXmlFilePath = staticDataFile,
				CacheXmlFilePath = cacheFile,
				SchemaFilePath = schemaFile,
				ValidateWhenCacheChanges = false,
			});
		var context = new GameServerRuntimeContext();
		context.SetDataManager(dataManager);
		return context;
	}
}
