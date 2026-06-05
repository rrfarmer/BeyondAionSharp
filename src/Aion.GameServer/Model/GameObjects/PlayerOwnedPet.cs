namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerOwnedPet(
	int ObjectId,
	int TemplateId,
	string Name,
	int Decoration);
