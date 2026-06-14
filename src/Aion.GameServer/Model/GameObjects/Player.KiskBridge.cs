namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Java parity bridge: the reworked KiskService runtime cleanup tracks the bound kisk by object id and
/// keeps a transient pending-bind request, where faithful Java <c>Player</c> holds a direct <c>Kisk</c>
/// reference (setKisk/getKisk) plus the shared <c>ResponseRequester</c>. These adapters let the reworked
/// PlayerKiskRegistry/PlayerKiskRemovalRuntimeCleanupService compile against the faithful spine.
/// </summary>
public partial class Player
{
    private int _boundKiskObjectId;

    // Java parity: services/KiskService binds/unbinds via Player.setKisk(Kisk)/getKisk(); the reworked
    // runtime registry decouples this to the kisk object id (0 == unbound).
    public int BoundKiskObjectId
    {
        get => _boundKiskObjectId;
        set => _boundKiskObjectId = value;
    }

    // Java parity: Player.getResponseRequester() — the SM_QUESTION_WINDOW request handler registry.
    public Aion.GameServer.Model.GameObjects.Players.ResponseRequester ResponseRequester => GetResponseRequester();

    // Reworked transient state mirroring the in-flight STR_ASK_REGISTER_BINDSTONE question; faithful Java
    // keeps the pending request inside ResponseRequester itself.
    public Aion.GameServer.Model.GameObjects.PendingKiskBindRequest? PendingKiskBindRequest { get; set; }
}
