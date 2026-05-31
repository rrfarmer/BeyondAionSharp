using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcDropBoostStatProviderReadinessReportServiceTests
{
	[Fact]
	public void CreateReport_RecordsMissingSkillTemplatesAndLiveProviders()
	{
		var report = WorldNpcDropBoostStatProviderReadinessReportService.CreateReport(null);

		Assert.Equal(WorldNpcDropBoostStatProviderReadinessStatus.MissingSkillTemplates, report.Status);
		Assert.False(report.IsReadyForWorkflow);
		Assert.Contains("skill_templates", report.MissingInputs);
		Assert.Contains("live effect state provider", report.MissingInputs);
		Assert.Contains("live CreatureGameStats provider", report.MissingInputs);
		Assert.Contains("CreatureGameStats.getStat", report.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateReport_DistinguishesAvailableStaticMetadataFromMissingLiveProviders()
	{
		var report = WorldNpcDropBoostStatProviderReadinessReportService.CreateReport(CreateDropBoostSkillTemplates());

		Assert.Equal(WorldNpcDropBoostStatProviderReadinessStatus.BlockedMissingLiveEffectStateProvider, report.Status);
		Assert.False(report.IsReadyForWorkflow);
		Assert.Equal(1, report.DropBoostEffectCount);
		Assert.Equal(1, report.DrBoostEffectCount);
		Assert.Equal(1, report.BoostDropRateChangeCount);
		Assert.Equal(1, report.DrBoostChangeCount);
		Assert.DoesNotContain("static boostdroprate BOOST_DROP_RATE metadata", report.MissingInputs);
		Assert.DoesNotContain("static drboost DR_BOOST metadata", report.MissingInputs);
		Assert.Contains("live effect state provider", report.MissingInputs);
		Assert.Contains("live CreatureGameStats provider", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_RequiresBothDropBoostStaticEffectKinds()
	{
		var templates = new SkillTemplateTable(
		[
			CreateTemplate(
				8472,
				[new SkillBuffStatEffectSummary("boostdroprate", [new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0)])])
		]);

		var report = WorldNpcDropBoostStatProviderReadinessReportService.CreateReport(
			templates,
			hasLiveEffectStateProvider: true,
			hasLiveCreatureGameStatsProvider: true);

		Assert.Equal(WorldNpcDropBoostStatProviderReadinessStatus.MissingStaticMetadata, report.Status);
		Assert.False(report.IsReadyForWorkflow);
		Assert.DoesNotContain("static boostdroprate BOOST_DROP_RATE metadata", report.MissingInputs);
		Assert.Contains("static drboost DR_BOOST metadata", report.MissingInputs);
		Assert.DoesNotContain("live effect state provider", report.MissingInputs);
		Assert.DoesNotContain("live CreatureGameStats provider", report.MissingInputs);
	}

	[Fact]
	public void CreateReport_IsReadyOnlyWhenStaticMetadataAndLiveProvidersArePresent()
	{
		var report = WorldNpcDropBoostStatProviderReadinessReportService.CreateReport(
			CreateDropBoostSkillTemplates(),
			hasLiveEffectStateProvider: true,
			hasLiveCreatureGameStatsProvider: true);

		Assert.Equal(WorldNpcDropBoostStatProviderReadinessStatus.Ready, report.Status);
		Assert.True(report.IsReadyForWorkflow);
		Assert.Empty(report.MissingInputs);
	}

	private static SkillTemplateTable CreateDropBoostSkillTemplates()
	{
		return new SkillTemplateTable(
		[
			CreateTemplate(
				8472,
				[new SkillBuffStatEffectSummary("boostdroprate", [new SkillStatChange("BOOST_DROP_RATE", "ADD", 20, 0)])]),
			CreateTemplate(
				9878,
				[new SkillBuffStatEffectSummary("drboost", [new SkillStatChange("DR_BOOST", "ADD", 100, 0)])])
		]);
	}

	private static SkillTemplateSummary CreateTemplate(int skillId, IReadOnlyList<SkillBuffStatEffectSummary> buffStatEffects)
	{
		return new SkillTemplateSummary(
			skillId,
			$"Skill {skillId}",
			0,
			1,
			string.Empty,
			string.Empty,
			"MAGICAL",
			"NONE",
			0,
			0,
			BuffStatEffectSummaries: buffStatEffects);
	}
}
