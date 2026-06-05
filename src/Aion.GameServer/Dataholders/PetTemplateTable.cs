using System.Collections.ObjectModel;
using Aion.GameServer.Model.Templates.Pet;

namespace Aion.GameServer.Dataholders;

public sealed class PetTemplateTable
{
	private readonly IReadOnlyDictionary<int, PetTemplateSummary> _templatesById;

	public PetTemplateTable(IReadOnlyList<PetTemplateSummary> templates)
	{
		Templates = templates;
		_templatesById = new ReadOnlyDictionary<int, PetTemplateSummary>(
			templates.ToDictionary(template => template.Id));
	}

	public IReadOnlyList<PetTemplateSummary> Templates { get; }

	public int Count => Templates.Count;

	public PetTemplateSummary? GetPetTemplate(int petId)
	{
		// Java parity: dataholders/PetData.getPetTemplate.
		return _templatesById.TryGetValue(petId, out var template) ? template : null;
	}

	public int? GetMerchantSellModifier(int petId)
	{
		return GetPetTemplate(petId)?.GetFunction(PetFunctionType.Merchant)?.RatePrice;
	}
}

public sealed record PetTemplateSummary(
	int Id,
	string Name,
	int NameId,
	int ConditionReward,
	IReadOnlyList<PetFunctionSummary> Functions)
{
	public bool ContainsFunction(PetFunctionType functionType)
	{
		// Java parity: model/templates/pet/PetTemplate.containsFunction.
		return Functions.Any(function => function.Type == functionType);
	}

	public PetFunctionSummary? GetFunction(PetFunctionType functionType)
	{
		// Java parity: model/templates/pet/PetTemplate.getPetFunction.
		return Functions.FirstOrDefault(function => function.Type == functionType);
	}
}

public sealed record PetFunctionSummary(
	int Id,
	PetFunctionType Type,
	int Slots,
	int RatePrice);
