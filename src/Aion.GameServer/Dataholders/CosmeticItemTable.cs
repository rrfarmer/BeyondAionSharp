using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class CosmeticItemTable
{
	private readonly IReadOnlyDictionary<string, CosmeticItemSummary> _templatesByName;

	public CosmeticItemTable(IReadOnlyList<CosmeticItemSummary> templates)
	{
		Templates = templates;
		var templatesByName = new Dictionary<string, CosmeticItemSummary>(StringComparer.Ordinal);
		foreach (var template in templates)
			templatesByName[template.CosmeticName] = template;
		_templatesByName = new ReadOnlyDictionary<string, CosmeticItemSummary>(templatesByName);
	}

	public IReadOnlyList<CosmeticItemSummary> Templates { get; }

	public int Count => Templates.Count;

	public CosmeticItemSummary? GetCosmeticItemTemplate(string cosmeticName)
	{
		// Java parity: dataholders/CosmeticItemsData.getCosmeticItemsTemplate.
		return _templatesByName.GetValueOrDefault(cosmeticName);
	}
}

// Java parity: model/templates/cosmeticitems/CosmeticItemTemplate.
public sealed record CosmeticItemSummary(
	string Type,
	string CosmeticName,
	int Id,
	string Race,
	string GenderPermitted,
	CosmeticPresetSummary? Preset);

// Java parity: model/templates/cosmeticitems/CosmeticItemTemplate.Preset.
public sealed record CosmeticPresetSummary(
	float Scale,
	int HairType,
	int FaceType,
	int HairColor,
	int LipColor,
	int EyeColor,
	int SkinColor);
