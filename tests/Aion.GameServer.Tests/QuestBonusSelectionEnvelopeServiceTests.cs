using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestBonusSelectionEnvelopeServiceTests
{
	[Fact]
	public void CreateEnvelope_ReportsJavaChanceInputsWithoutSelectingGroupOrItem()
	{
		var plan = new QuestBonusCandidatePlan(
			new QuestBonusCandidatePlanInput("MEDAL", 50, "ELYOS"),
			[
				new QuestBonusCandidateGroupDescriptor(
					"medals",
					"MEDAL",
					70f,
					QuestBonusItemShape.FullRewardItem,
					[
						new QuestBonusCandidateItemDescriptor(
							186000030,
							XmlRace: null,
							TemplateRace: "PC_ALL",
							TemplateLevel: 1,
							XmlLevel: 50,
							Skill: null,
							MinLevel: null,
							MaxLevel: null,
							EffectiveChance: 25f,
							CountMin: 2,
							CountMax: 2,
							QuestBonusCandidateCountMode.Fixed),
					]),
				new QuestBonusCandidateGroupDescriptor(
					"events",
					"MEDAL",
					30f,
					QuestBonusItemShape.FullRewardItem,
					[
						new QuestBonusCandidateItemDescriptor(
							188000001,
							XmlRace: null,
							TemplateRace: "PC_ALL",
							TemplateLevel: 1,
							XmlLevel: 50,
							Skill: null,
							MinLevel: null,
							MaxLevel: null,
							EffectiveChance: 75f,
							CountMin: 1,
							CountMax: 1,
							QuestBonusCandidateCountMode.Fixed),
					]),
			],
			[
				new QuestBonusSkippedItemDescriptor(
					"medals",
					"MEDAL",
					QuestBonusItemShape.FullRewardItem,
					186000031,
					QuestBonusCandidateSkipReason.BonusLevelMismatch,
					XmlRace: null,
					TemplateRace: "PC_ALL",
					TemplateLevel: 1,
					XmlLevel: 55,
					Skill: null,
					MinLevel: null,
					MaxLevel: null),
			]);
		var service = new QuestBonusSelectionEnvelopeService();

		var envelope = service.CreateEnvelope(plan);

		Assert.Equal(QuestBonusSelectionEnvelopeStatus.SelectionInputsAvailable, envelope.Status);
		Assert.Equal(100f, envelope.GroupChanceSum);
		Assert.Equal(1, envelope.SkippedItemCount);
		Assert.Collection(
			envelope.Groups,
			group =>
			{
				Assert.Equal("medals", group.ElementName);
				Assert.Equal(70f, group.GroupChance);
				Assert.Equal(25f, group.ItemChanceSum);
				Assert.Equal(QuestBonusSelectionGroupStatus.ItemChanceInputsAvailable, group.Status);
				var item = Assert.Single(group.Items);
				Assert.Equal(186000030, item.ItemId);
				Assert.Equal(25f, item.ItemChance);
				Assert.Equal(2L, item.CountMin);
				Assert.Equal(2L, item.CountMax);
			},
			group =>
			{
				Assert.Equal("events", group.ElementName);
				Assert.Equal(30f, group.GroupChance);
				Assert.Equal(75f, group.ItemChanceSum);
			});
	}

	[Fact]
	public void CreateEnvelope_ReportsNoCandidateGroupsLikeJavaBonusServiceNullResult()
	{
		var plan = new QuestBonusCandidatePlan(
			new QuestBonusCandidatePlanInput("TASK", 0, "ELYOS", CombineSkill: 40007, CombineSkillPoint: 50),
			[],
			[]);
		var service = new QuestBonusSelectionEnvelopeService();

		var envelope = service.CreateEnvelope(plan);

		Assert.Equal(QuestBonusSelectionEnvelopeStatus.NoCandidateGroups, envelope.Status);
		Assert.Equal(0f, envelope.GroupChanceSum);
		Assert.Empty(envelope.Groups);
	}

	[Fact]
	public void CreateEnvelope_ReportsNoPositiveGroupChanceLikeJavaChanceNullSelection()
	{
		var plan = new QuestBonusCandidatePlan(
			new QuestBonusCandidatePlanInput("EVENTS", 1, "ELYOS"),
			[
				new QuestBonusCandidateGroupDescriptor(
					"events",
					"EVENTS",
					0f,
					QuestBonusItemShape.FullRewardItem,
					[
						new QuestBonusCandidateItemDescriptor(
							188000001,
							XmlRace: null,
							TemplateRace: "PC_ALL",
							TemplateLevel: 1,
							XmlLevel: 1,
							Skill: null,
							MinLevel: null,
							MaxLevel: null,
							EffectiveChance: 100f,
							CountMin: 1,
							CountMax: 1,
							QuestBonusCandidateCountMode.Fixed),
					]),
			],
			[]);
		var service = new QuestBonusSelectionEnvelopeService();

		var envelope = service.CreateEnvelope(plan);

		Assert.Equal(QuestBonusSelectionEnvelopeStatus.NoPositiveGroupChance, envelope.Status);
		Assert.Equal(0f, envelope.GroupChanceSum);
		Assert.Single(envelope.Groups);
	}

	[Fact]
	public void CreateEnvelope_ReportsGroupWithNoPositiveItemChanceLikeJavaItemSelectionNull()
	{
		var plan = new QuestBonusCandidatePlan(
			new QuestBonusCandidatePlanInput("MEDAL", 50, "ELYOS"),
			[
				new QuestBonusCandidateGroupDescriptor(
					"medals",
					"MEDAL",
					100f,
					QuestBonusItemShape.FullRewardItem,
					[
						new QuestBonusCandidateItemDescriptor(
							186000030,
							XmlRace: null,
							TemplateRace: "PC_ALL",
							TemplateLevel: 1,
							XmlLevel: 50,
							Skill: null,
							MinLevel: null,
							MaxLevel: null,
							EffectiveChance: 0f,
							CountMin: 2,
							CountMax: 2,
							QuestBonusCandidateCountMode.Fixed),
					]),
			],
			[]);
		var service = new QuestBonusSelectionEnvelopeService();

		var envelope = service.CreateEnvelope(plan);

		Assert.Equal(QuestBonusSelectionEnvelopeStatus.HasGroupWithNoPositiveItemChance, envelope.Status);
		var group = Assert.Single(envelope.Groups);
		Assert.Equal(QuestBonusSelectionGroupStatus.NoPositiveItemChance, group.Status);
		Assert.Equal(0f, group.ItemChanceSum);
	}
}
