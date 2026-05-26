using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishCustomRewardSessionRuntimeInputAdapterServiceTests
{
	[Fact]
	public void CreateOptions_DisabledGateDoesNotRequireSessionDependenciesOrAllocateIds()
	{
		var idFactory = new IDFactory([1, 2]);

		var result = QuestFinishCustomRewardSessionRuntimeInputAdapterService.CreateOptions(
			new QuestFinishCustomRewardSessionRuntimeInputAdapterInput(
				EnableCustomRewardExecution: false,
				CreateOptions("UTC"),
				IdFactory: idFactory));

		Assert.Equal(QuestFinishCustomRewardRuntimeInputAssemblerStatus.Disabled, result.Status);
		Assert.False(result.Options.EnableCustomRewardExecution);
		Assert.Equal(3, idFactory.NextId());
	}

	[Theory]
	[InlineData(false, true, true, "accountCreationEpochMillis")]
	[InlineData(true, false, true, "nextObjectId")]
	[InlineData(true, true, false, "itemTemplates")]
	public void CreateOptions_EnabledGateRequiresPlayerIdFactoryAndStaticItemTemplates(
		bool includeAccountCreation,
		bool includeIdFactory,
		bool includeItemTemplates,
		string expectedMissingDependency)
	{
		var result = QuestFinishCustomRewardSessionRuntimeInputAdapterService.CreateOptions(
			new QuestFinishCustomRewardSessionRuntimeInputAdapterInput(
				EnableCustomRewardExecution: true,
				CreateOptions("UTC"),
				Player: CreatePlayer(includeAccountCreation ? 1_655_510_400_000L : null),
				IdFactory: includeIdFactory ? new IDFactory([1, 2]) : null,
				ReceivedTime: new DateTime(2026, 5, 26, 16, 30, 0),
				ItemTemplates: includeItemTemplates ? CreateItemTemplates() : null));

		Assert.Equal(QuestFinishCustomRewardRuntimeInputAssemblerStatus.MissingDependency, result.Status);
		Assert.Equal(expectedMissingDependency, result.MissingDependency);
		Assert.False(result.Options.EnableCustomRewardExecution);
	}

	[Fact]
	public void CreateOptions_CreatesAssemblerOptionsFromActivePlayerAndRuntimeDependencies()
	{
		var idFactory = new IDFactory([1, 2]);
		var itemTemplates = CreateItemTemplates();
		var receivedTime = new DateTime(2026, 5, 26, 16, 30, 0);

		var result = QuestFinishCustomRewardSessionRuntimeInputAdapterService.CreateOptions(
			new QuestFinishCustomRewardSessionRuntimeInputAdapterInput(
				EnableCustomRewardExecution: true,
				CreateOptions("UTC"),
				Player: CreatePlayer(1_655_510_400_000L),
				IdFactory: idFactory,
				ReceivedTime: receivedTime,
				ItemTemplates: itemTemplates));

		Assert.Equal(QuestFinishCustomRewardRuntimeInputAssemblerStatus.Created, result.Status);
		Assert.True(result.Applied);
		Assert.True(result.Options.EnableCustomRewardExecution);
		Assert.Equal(3, result.Options.NextObjectId?.Invoke());
		Assert.Equal(4, idFactory.NextId());
		Assert.Equal(receivedTime, result.Options.ReceivedTime);
		Assert.Equal(new DateTime(2022, 6, 18, 0, 0, 0), result.Options.FactionPackAccountCreationLocalTime);
		Assert.Same(itemTemplates, result.Options.ItemTemplates);
	}

	private static Player CreatePlayer(long? accountCreationEpochMillis)
	{
		return new Player
		{
			ObjectId = 4701,
			AccountId = 3301,
			Name = "Questfinish",
			Race = "ASMODIANS",
			PlayerClass = "RANGER",
			Level = 65,
			AccountCreationEpochMillis = accountCreationEpochMillis,
		};
	}

	private static GameServerOptions CreateOptions(string timeZoneId)
	{
		return new GameServerOptions
		{
			Core = new GameServerCoreOptions
			{
				TimeZoneId = timeZoneId,
			},
		};
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(186000236, "Blood Mark", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 100, 0, 0),
		]);
	}
}
