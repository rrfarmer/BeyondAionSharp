using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

public sealed record PendingRecallInstantRequest(
	int EffectorObjectId,
	string EffectorName,
	int EffectedObjectId,
	string EffectedName,
	WorldPosition Destination,
	int QuestionId);
