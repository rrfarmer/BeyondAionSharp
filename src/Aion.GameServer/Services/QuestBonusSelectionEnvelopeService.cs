namespace Aion.GameServer.Services;

public sealed class QuestBonusSelectionEnvelopeService
{
	public QuestBonusSelectionEnvelope CreateEnvelope(QuestBonusCandidatePlan candidatePlan)
	{
		ArgumentNullException.ThrowIfNull(candidatePlan);

		// Java parity: model/Chance#selectElement plus
		// services/reward/BonusService#getQuestBonus. This service only reports the
		// Chance input surface; it never rolls RNG or creates QuestItems.
		var groups = candidatePlan
			.CandidateGroups
			.Select(CreateGroup)
			.ToArray();
		var groupChanceSum = groups.Sum(group => group.GroupChance);
		var status = GetStatus(groups, groupChanceSum);

		return new QuestBonusSelectionEnvelope(
			candidatePlan.Input,
			status,
			groupChanceSum,
			groups,
			candidatePlan.SkippedItems.Count);
	}

	private static QuestBonusSelectionEnvelopeStatus GetStatus(
		IReadOnlyList<QuestBonusSelectionGroupEnvelope> groups,
		float groupChanceSum)
	{
		if (groups.Count == 0)
			return QuestBonusSelectionEnvelopeStatus.NoCandidateGroups;
		if (groupChanceSum <= 0f)
			return QuestBonusSelectionEnvelopeStatus.NoPositiveGroupChance;
		if (groups.Any(group => group.Status == QuestBonusSelectionGroupStatus.NoPositiveItemChance))
			return QuestBonusSelectionEnvelopeStatus.HasGroupWithNoPositiveItemChance;

		return QuestBonusSelectionEnvelopeStatus.SelectionInputsAvailable;
	}

	private static QuestBonusSelectionGroupEnvelope CreateGroup(QuestBonusCandidateGroupDescriptor group)
	{
		var items = group.Items.Select(CreateItem).ToArray();
		var itemChanceSum = items.Sum(item => item.ItemChance);
		var status = itemChanceSum > 0f
			? QuestBonusSelectionGroupStatus.ItemChanceInputsAvailable
			: QuestBonusSelectionGroupStatus.NoPositiveItemChance;

		return new QuestBonusSelectionGroupEnvelope(
			group.ElementName,
			group.BonusType,
			group.Chance,
			itemChanceSum,
			group.ItemShape,
			status,
			items);
	}

	private static QuestBonusSelectionItemEnvelope CreateItem(QuestBonusCandidateItemDescriptor item) =>
		new(
			item.ItemId,
			item.EffectiveChance,
			item.CountMin,
			item.CountMax,
			item.CountMode);
}

public sealed record QuestBonusSelectionEnvelope(
	QuestBonusCandidatePlanInput Input,
	QuestBonusSelectionEnvelopeStatus Status,
	float GroupChanceSum,
	IReadOnlyList<QuestBonusSelectionGroupEnvelope> Groups,
	int SkippedItemCount);

public sealed record QuestBonusSelectionGroupEnvelope(
	string ElementName,
	string BonusType,
	float GroupChance,
	float ItemChanceSum,
	QuestBonusItemShape ItemShape,
	QuestBonusSelectionGroupStatus Status,
	IReadOnlyList<QuestBonusSelectionItemEnvelope> Items);

public sealed record QuestBonusSelectionItemEnvelope(
	int ItemId,
	float ItemChance,
	long CountMin,
	long CountMax,
	QuestBonusCandidateCountMode CountMode);

public enum QuestBonusSelectionEnvelopeStatus
{
	NoCandidateGroups,
	NoPositiveGroupChance,
	HasGroupWithNoPositiveItemChance,
	SelectionInputsAvailable,
}

public enum QuestBonusSelectionGroupStatus
{
	NoPositiveItemChance,
	ItemChanceInputsAvailable,
}
