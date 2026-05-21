using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class SkillTemplateTable
{
	private readonly IReadOnlyDictionary<int, SkillTemplateSummary> _templatesById;
	private readonly IReadOnlyDictionary<string, IReadOnlyList<SkillTemplateSummary>> _templatesByGroup;
	private readonly IReadOnlyDictionary<string, IReadOnlyList<SkillTemplateSummary>> _templatesByStack;

	public SkillTemplateTable(IReadOnlyList<SkillTemplateSummary> templates)
	{
		Templates = templates;
		_templatesById = new ReadOnlyDictionary<int, SkillTemplateSummary>(
			templates.ToDictionary(template => template.SkillId));
		_templatesByGroup = IndexByOptionalString(templates, template => template.Group);
		_templatesByStack = IndexByOptionalString(templates, template => template.Stack);
	}

	public IReadOnlyList<SkillTemplateSummary> Templates { get; }

	public int Count => Templates.Count;

	public SkillTemplateSummary? GetSkillTemplate(int skillId)
	{
		return _templatesById.GetValueOrDefault(skillId);
	}

	public IReadOnlyList<SkillTemplateSummary> GetSkillTemplatesByGroup(string skillGroup)
	{
		return _templatesByGroup.GetValueOrDefault(skillGroup) ?? Array.Empty<SkillTemplateSummary>();
	}

	public IReadOnlyList<SkillTemplateSummary> GetSkillTemplatesByStack(string skillStack)
	{
		return _templatesByStack.GetValueOrDefault(skillStack) ?? Array.Empty<SkillTemplateSummary>();
	}

	private static IReadOnlyDictionary<string, IReadOnlyList<SkillTemplateSummary>> IndexByOptionalString(
		IReadOnlyList<SkillTemplateSummary> templates,
		Func<SkillTemplateSummary, string> keySelector)
	{
		var valuesByKey = new Dictionary<string, List<SkillTemplateSummary>>(StringComparer.Ordinal);
		foreach (var template in templates)
		{
			var key = keySelector(template);
			if (string.IsNullOrEmpty(key))
				continue;

			if (!valuesByKey.TryGetValue(key, out var values))
			{
				values = [];
				valuesByKey[key] = values;
			}

			values.Add(template);
		}

		return new ReadOnlyDictionary<string, IReadOnlyList<SkillTemplateSummary>>(
			valuesByKey.ToDictionary(
				pair => pair.Key,
				pair => (IReadOnlyList<SkillTemplateSummary>) pair.Value.AsReadOnly(),
				StringComparer.Ordinal));
	}
}

public sealed record SkillTemplateSummary(
	int SkillId,
	string Name,
	int NameId,
	int Level,
	string Group,
	string Stack,
	string SkillType,
	string SkillSubType,
	int CooldownId,
	int Cooldown,
	IReadOnlyList<SkillArmorMasteryEffectSummary>? ArmorMasteryEffects = null,
	IReadOnlyList<SkillWeaponMasteryEffectSummary>? WeaponMasteryEffects = null,
	IReadOnlyList<SkillShieldMasteryEffectSummary>? ShieldMasteryEffects = null,
	IReadOnlyList<SkillWeaponDualEffectSummary>? WeaponDualEffects = null,
	string StigmaType = "")
{
	public IReadOnlyList<SkillArmorMasteryEffectSummary> ArmorMastery => ArmorMasteryEffects ?? Array.Empty<SkillArmorMasteryEffectSummary>();

	public IReadOnlyList<SkillWeaponMasteryEffectSummary> WeaponMastery => WeaponMasteryEffects ?? Array.Empty<SkillWeaponMasteryEffectSummary>();

	public IReadOnlyList<SkillShieldMasteryEffectSummary> ShieldMastery => ShieldMasteryEffects ?? Array.Empty<SkillShieldMasteryEffectSummary>();

	public IReadOnlyList<SkillWeaponDualEffectSummary> WeaponDual => WeaponDualEffects ?? Array.Empty<SkillWeaponDualEffectSummary>();

	public bool IsStigmaSkill => !string.IsNullOrEmpty(StigmaType) && !string.Equals(StigmaType, "NONE", StringComparison.Ordinal);

	public string? GetClientName()
	{
		// Java parity: model/templates/L10n.getL10n -> utils/ChatUtil.l10n.
		if (NameId == 0)
			return null;

		var l10nId = (NameId << 1) | 1;
		return string.Concat("$", (char)(l10nId & 0xffff), (char)((l10nId >>> 16) & 0xffff));
	}
}

// Java parity: skillengine/effect/ArmorMasteryEffect.
public sealed record SkillArmorMasteryEffectSummary(
	string ArmorType,
	int Value,
	int Delta,
	IReadOnlyList<SkillStatChange> Changes);

// Java parity: skillengine/effect/WeaponMasteryEffect.
public sealed record SkillWeaponMasteryEffectSummary(string WeaponGroup, IReadOnlyList<SkillStatChange> Changes);

// Java parity: skillengine/effect/ShieldMasteryEffect.
public sealed record SkillShieldMasteryEffectSummary(IReadOnlyList<SkillStatChange> Changes);

// Java parity: skillengine/effect/WeaponDualEffect.
public sealed record SkillWeaponDualEffectSummary(int Value, int Delta, int SkillEfficiency, int MaxDamageChance, int MaxDamageDelta);

// Java parity: skillengine/change/Change entries under passive skill effects.
public sealed record SkillStatChange(string Stat, string Func, int Value, int Delta);
