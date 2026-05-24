using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class PetSkillTable
{
	private readonly IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> _skillIdsByPetIdByOrderSkill;
	private readonly IReadOnlyDictionary<int, IReadOnlySet<int>> _skillIdsByPetId;

	public PetSkillTable(IReadOnlyList<PetSkillSummary> petSkills)
	{
		PetSkills = petSkills;
		var skillIdsByPetIdByOrderSkill = new Dictionary<int, Dictionary<int, int>>();
		var skillIdsByPetId = new Dictionary<int, HashSet<int>>();
		foreach (var petSkill in petSkills)
		{
			if (!skillIdsByPetIdByOrderSkill.TryGetValue(petSkill.OrderSkillId, out var skillIdsByPetIdForOrder))
			{
				skillIdsByPetIdForOrder = [];
				skillIdsByPetIdByOrderSkill[petSkill.OrderSkillId] = skillIdsByPetIdForOrder;
			}

			skillIdsByPetIdForOrder[petSkill.PetId] = petSkill.SkillId;

			if (!skillIdsByPetId.TryGetValue(petSkill.PetId, out var skillIdsForPet))
			{
				skillIdsForPet = [];
				skillIdsByPetId[petSkill.PetId] = skillIdsForPet;
			}

			skillIdsForPet.Add(petSkill.SkillId);
		}

		_skillIdsByPetIdByOrderSkill = new ReadOnlyDictionary<int, IReadOnlyDictionary<int, int>>(
			skillIdsByPetIdByOrderSkill.ToDictionary(
				pair => pair.Key,
				pair => (IReadOnlyDictionary<int, int>) new ReadOnlyDictionary<int, int>(pair.Value)));
		_skillIdsByPetId = new ReadOnlyDictionary<int, IReadOnlySet<int>>(
			skillIdsByPetId.ToDictionary(
				pair => pair.Key,
				pair => (IReadOnlySet<int>) pair.Value));
	}

	public IReadOnlyList<PetSkillSummary> PetSkills { get; }

	public int Count => _skillIdsByPetIdByOrderSkill.Count;

	public bool IsPetOrderSkill(int orderSkillId)
	{
		// Java parity: dataholders/PetSkillData.isPetOrderSkill.
		return _skillIdsByPetIdByOrderSkill.ContainsKey(orderSkillId);
	}

	public int? GetPetOrderSkill(int orderSkillId, int petNpcId)
	{
		// Java parity: dataholders/PetSkillData.getPetOrderSkill, nullable until full Java exception behavior is needed.
		return _skillIdsByPetIdByOrderSkill.TryGetValue(orderSkillId, out var skillIdsByPetId)
			&& skillIdsByPetId.TryGetValue(petNpcId, out var skillId)
				? skillId
				: null;
	}

	public bool PetHasSkill(int petNpcId, int skillId)
	{
		// Java parity: dataholders/PetSkillData.petHasSkill.
		return _skillIdsByPetId.TryGetValue(petNpcId, out var skillIds) && skillIds.Contains(skillId);
	}
}

public sealed record PetSkillSummary(int SkillId, int PetId, int OrderSkillId);
