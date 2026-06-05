namespace Aion.GameServer.Services.ToyPet;

public sealed class PetFeedDataTable
{
	public PetFeedDataTable(PetFeedEvaluationContext context)
	{
		Context = context ?? throw new ArgumentNullException(nameof(context));
	}

	public PetFeedEvaluationContext Context { get; }

	public IReadOnlyDictionary<int, PetFeedFlavourProjection> Flavours => Context.Flavours;

	public int Count => Context.Flavours.Count;
}
