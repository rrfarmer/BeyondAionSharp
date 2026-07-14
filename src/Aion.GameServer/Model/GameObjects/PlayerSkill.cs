namespace Aion.GameServer.Model.GameObjects;

public sealed class PlayerSkill
{
	public int SkillId { get; init; }

	public int SkillLevel { get; init; }

	public int SkillType { get; init; }

	public int CurrentXp { get; init; }

	public bool IsStigmaSkill => SkillType > 0;

	public bool IsNormalSkill => !IsStigmaSkill && SkillId < 30000;

	public bool IsTappingSkill => SkillId is >= 30001 and <= 30003;

	public bool IsCraftingSkill => SkillId is >= 40001 and <= 40010 && !IsMorphSkill;

	public bool IsMorphSkill => SkillId == 40009;

	public bool IsProfessionSkill => SkillId is >= 30000 and < 50000;

	public int GetClientSkillLevel()
	{
		// Java parity: model/skill/PlayerSkillEntry.isNormalSkill in SkillEntryWriter.writeMe.
		return IsNormalSkill ? 1 : SkillLevel;
	}

	public int GetProfessionSkillBarSize()
	{
		// Java parity: model/skill/PlayerSkillEntry.getProfessionSkillBarSize.
		if (!IsProfessionSkill)
			return 0;

		var size = SkillLevel / 100;
		if (IsCraftingSkill && SkillLevel >= 450)
			size += (SkillLevel - 350) / 100;

		return IsTappingSkill ? Math.Min(size, 4) : size;
	}

	public int GetClientFlag(DateTimeOffset now)
	{
		// Java parity: PlayerSkillEntry.getProfessionFlag/getFlag.
		if (IsProfessionSkill)
		{
			if (IsTappingSkill || IsMorphSkill)
				return 1;
			return IsCraftingSkill ? CurrentXp : 0;
		}

		return IsNormalSkill ? unchecked((int)now.ToUnixTimeSeconds()) : 0;
	}
}
