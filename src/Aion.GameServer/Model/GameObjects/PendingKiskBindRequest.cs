namespace Aion.GameServer.Model.GameObjects;

public sealed record PendingKiskBindRequest(int KiskObjectId, int QuestionId);

public sealed record PendingBeshmundirDifficultyEnterRequest(int NpcObjectId, int DialogActionId, int PathL10nId);
