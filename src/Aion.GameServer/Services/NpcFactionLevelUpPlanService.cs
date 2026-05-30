using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class NpcFactionLevelUpPlanService
{
	// Java parity: model/gameobjects/player/NpcFactions.onLevelUp evaluates the active normal and
	// mentor faction slots against DataManager.NPC_FACTIONS_DATA, clearing factions that exceed maxLevel.
	public const int FactionLeaveByLevelLimitSystemMessageId = 1400770;

	public static NpcFactionLevelUpPlan CreatePlan(PlayerNpcFactionsSnapshot? npcFactions, int playerLevel, NpcFactionTable? npcFactionTable)
	{
		// Java parity: onLevelUp keeps in-range factions, leaves out-of-range factions, abandons START-state
		// quests for removed factions, and emits the level-limit leave system message in the live path.
		if (npcFactions == null)
			return NpcFactionLevelUpPlan.MissingSnapshot();

		var activeSlots = new[]
		{
			new NpcFactionLevelUpSlot(IsMentorSlot: false, npcFactions.GetActiveFaction(isMentor: false)),
			new NpcFactionLevelUpSlot(IsMentorSlot: true, npcFactions.GetActiveFaction(isMentor: true)),
		};
		if (activeSlots.All(slot => slot.Faction is not { IsActive: true }))
		{
			return new NpcFactionLevelUpPlan(
				NpcFactionLevelUpPlanStatus.NoActiveFactions,
				npcFactions,
				npcFactions,
				Array.Empty<NpcFactionLevelUpDescriptor>()
			);
		}

		if (npcFactionTable == null)
		{
			return new NpcFactionLevelUpPlan(
				NpcFactionLevelUpPlanStatus.BlockedMissingTemplate,
				npcFactions,
				npcFactions,
				activeSlots
					.Where(slot => slot.Faction is { IsActive: true })
					.Select(slot =>
						MissingTemplateDescriptor(slot, "Static NPC faction table is unavailable; Java would require DataManager.NPC_FACTIONS_DATA.")
					)
					.ToArray()
			);
		}

		var descriptors = new List<NpcFactionLevelUpDescriptor>();
		var replacements = new Dictionary<int, PlayerNpcFactionState>();
		foreach (var slot in activeSlots)
		{
			var faction = slot.Faction;
			if (faction is not { IsActive: true })
				continue;

			var template = npcFactionTable.GetNpcFactionById(faction.FactionId);
			if (template == null)
			{
				return new NpcFactionLevelUpPlan(
					NpcFactionLevelUpPlanStatus.BlockedMissingTemplate,
					npcFactions,
					npcFactions,
					descriptors
						.Append(MissingTemplateDescriptor(slot, "Static NPC faction template is missing; Java would dereference the template during onLevelUp."))
						.ToArray()
				);
			}

			if (template.MaxLevel >= playerLevel)
			{
				descriptors.Add(
					new NpcFactionLevelUpDescriptor(
						slot.IsMentorSlot,
						faction.FactionId,
						NpcFactionLevelUpDescriptorStatus.WithinLevelLimit,
						faction,
						PlannedFaction: faction,
						template.MaxLevel,
						template.NameId,
						template.Name,
						QuestIdToAbandon: null,
						SystemMessageId: null,
						"NpcFactions.onLevelUp",
						Notes: "Java keeps active factions when template maxLevel is greater than or equal to the player's new level."
					)
				);
				continue;
			}

			var planned = faction with { IsActive = false, State = PlayerNpcFactionQuestState.Noting };
			replacements[faction.FactionId] = planned;
			descriptors.Add(
				new NpcFactionLevelUpDescriptor(
					slot.IsMentorSlot,
					faction.FactionId,
					NpcFactionLevelUpDescriptorStatus.PlannedLeaveByLevelLimit,
					faction,
					planned,
					template.MaxLevel,
					template.NameId,
					template.Name,
					QuestIdToAbandon: faction.State == PlayerNpcFactionQuestState.Start ? faction.QuestId : null,
					SystemMessageId: FactionLeaveByLevelLimitSystemMessageId,
					"NpcFactions.onLevelUp -> SM_SYSTEM_MESSAGE.STR_FACTION_LEAVE_BY_LEVEL_LIMIT",
					Notes: "Future live execution must clear the active slot, abandon a START-state faction quest, send the level-limit leave system message, and persist the faction update."
				)
			);
		}

		if (replacements.Count == 0)
		{
			return new NpcFactionLevelUpPlan(NpcFactionLevelUpPlanStatus.NoChanges, npcFactions, npcFactions, descriptors);
		}

		var updatedFactions = npcFactions.Factions.Select(faction => replacements.GetValueOrDefault(faction.FactionId, faction)).ToArray();
		var plannedSnapshot = new PlayerNpcFactionsSnapshot(updatedFactions);
		return new NpcFactionLevelUpPlan(NpcFactionLevelUpPlanStatus.Applied, npcFactions, plannedSnapshot, descriptors);
	}

	private static NpcFactionLevelUpDescriptor MissingTemplateDescriptor(NpcFactionLevelUpSlot slot, string notes)
	{
		var factionId = slot.Faction?.FactionId ?? 0;
		return new NpcFactionLevelUpDescriptor(
			slot.IsMentorSlot,
			factionId,
			NpcFactionLevelUpDescriptorStatus.MissingTemplate,
			slot.Faction,
			PlannedFaction: slot.Faction,
			TemplateMaxLevel: null,
			TemplateNameId: null,
			TemplateName: null,
			QuestIdToAbandon: null,
			SystemMessageId: null,
			"NpcFactions.onLevelUp -> DataManager.NPC_FACTIONS_DATA.getNpcFactionById",
			Notes: notes
		);
	}

	private sealed record NpcFactionLevelUpSlot(bool IsMentorSlot, PlayerNpcFactionState? Faction);
}

public sealed record NpcFactionLevelUpPlan(
	NpcFactionLevelUpPlanStatus Status,
	PlayerNpcFactionsSnapshot PreviousSnapshot,
	PlayerNpcFactionsSnapshot PlannedSnapshot,
	IReadOnlyList<NpcFactionLevelUpDescriptor> Descriptors
)
{
	public bool Applied => Status == NpcFactionLevelUpPlanStatus.Applied;

	public static NpcFactionLevelUpPlan MissingSnapshot()
	{
		return new NpcFactionLevelUpPlan(
			NpcFactionLevelUpPlanStatus.MissingSnapshot,
			PlayerNpcFactionsSnapshot.Empty,
			PlayerNpcFactionsSnapshot.Empty,
			Array.Empty<NpcFactionLevelUpDescriptor>()
		);
	}
}

public sealed record NpcFactionLevelUpDescriptor(
	bool IsMentorSlot,
	int FactionId,
	NpcFactionLevelUpDescriptorStatus Status,
	PlayerNpcFactionState? PreviousFaction,
	PlayerNpcFactionState? PlannedFaction,
	int? TemplateMaxLevel,
	int? TemplateNameId,
	string? TemplateName,
	int? QuestIdToAbandon,
	int? SystemMessageId,
	string JavaSource,
	bool IsLive = false,
	string? Notes = null
);

public enum NpcFactionLevelUpPlanStatus
{
	Applied,
	NoActiveFactions,
	NoChanges,
	MissingSnapshot,
	BlockedMissingTemplate,
}

public enum NpcFactionLevelUpDescriptorStatus
{
	PlannedLeaveByLevelLimit,
	WithinLevelLimit,
	MissingTemplate,
}
