namespace Aion.GameServer.Model.Legion;

public sealed record LegionEmblemSnapshot(
	int LegionId,
	string LegionName,
	byte EmblemId,
	byte EmblemType,
	byte ColorA,
	byte ColorR,
	byte ColorG,
	byte ColorB,
	byte[] CustomEmblemData);
