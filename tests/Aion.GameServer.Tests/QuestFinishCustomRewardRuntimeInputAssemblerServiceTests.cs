using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestFinishCustomRewardRuntimeInputAssemblerServiceTests
{
	[Fact]
	public void CreateOptions_DisabledGateReturnsDisabledAdapterOptionsWithoutDependencies()
	{
		var result = QuestFinishCustomRewardRuntimeInputAssemblerService.CreateOptions(
			new QuestFinishCustomRewardRuntimeInputAssemblerInput(
				EnableCustomRewardExecution: false,
				CreateOptions("UTC")));

		Assert.Equal(QuestFinishCustomRewardRuntimeInputAssemblerStatus.Disabled, result.Status);
		Assert.False(result.Applied);
		Assert.False(result.Options.EnableCustomRewardExecution);
		Assert.Null(result.Options.NextObjectId);
		Assert.Null(result.Options.ItemTemplates);
	}

	[Theory]
	[InlineData(null, true, true, "accountCreationEpochMillis")]
	[InlineData(1_592_096_400_000L, false, true, "nextObjectId")]
	[InlineData(1_592_096_400_000L, true, false, "itemTemplates")]
	public void CreateOptions_EnabledGateRequiresRuntimeInputsBeforeAdapterExecution(
		long? accountCreationEpochMillis,
		bool includeNextObjectId,
		bool includeItemTemplates,
		string expectedMissingDependency)
	{
		var result = QuestFinishCustomRewardRuntimeInputAssemblerService.CreateOptions(
			new QuestFinishCustomRewardRuntimeInputAssemblerInput(
				EnableCustomRewardExecution: true,
				CreateOptions("UTC"),
				accountCreationEpochMillis,
				includeNextObjectId ? () => 9001 : null,
				new DateTime(2026, 5, 26, 15, 30, 0),
				includeItemTemplates ? CreateItemTemplates() : null));

		Assert.Equal(QuestFinishCustomRewardRuntimeInputAssemblerStatus.MissingDependency, result.Status);
		Assert.Equal(expectedMissingDependency, result.MissingDependency);
		Assert.False(result.Options.EnableCustomRewardExecution);
		Assert.Null(result.Options.NextObjectId);
		Assert.Null(result.Options.ItemTemplates);
	}

	[Fact]
	public void CreateOptions_CreatesAdapterOptionsWithJavaServerTimeEpochMillisConversion()
	{
		const long asmodianWindowStartUtcMillis = 1_655_510_400_000L;
		var ids = new Queue<int>([9001]);
		var itemTemplates = CreateItemTemplates();
		var receivedTime = new DateTime(2026, 5, 26, 15, 30, 0);

		var result = QuestFinishCustomRewardRuntimeInputAssemblerService.CreateOptions(
			new QuestFinishCustomRewardRuntimeInputAssemblerInput(
				EnableCustomRewardExecution: true,
				CreateOptions("UTC"),
				AccountCreationEpochMillis: asmodianWindowStartUtcMillis,
				NextObjectId: () => ids.Dequeue(),
				ReceivedTime: receivedTime,
				ItemTemplates: itemTemplates));

		Assert.Equal(QuestFinishCustomRewardRuntimeInputAssemblerStatus.Created, result.Status);
		Assert.True(result.Applied);
		Assert.True(result.Options.EnableCustomRewardExecution);
		Assert.Equal(9001, result.Options.NextObjectId?.Invoke());
		Assert.Equal(receivedTime, result.Options.ReceivedTime);
		Assert.Equal(new DateTime(2022, 6, 18, 0, 0, 0), result.Options.FactionPackAccountCreationLocalTime);
		Assert.Same(itemTemplates, result.Options.ItemTemplates);
	}

	[Fact]
	public void ConvertEpochMillisToServerLocalTime_MatchesJavaServerTimeOfEpochMilliAcrossOffsets()
	{
		var eastern = TimeZoneInfo.CreateCustomTimeZone(
			"JavaServerEastern",
			TimeSpan.FromHours(-4),
			"JavaServerEastern",
			"JavaServerEastern");

		var localTime = QuestFinishCustomRewardRuntimeInputAssemblerService.ConvertEpochMillisToServerLocalTime(
			1_655_510_400_000L,
			eastern);

		Assert.Equal(new DateTime(2022, 6, 17, 20, 0, 0), localTime);
		Assert.Equal(DateTimeKind.Unspecified, localTime.Kind);
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
