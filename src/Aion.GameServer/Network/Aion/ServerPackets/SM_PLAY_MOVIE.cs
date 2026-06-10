using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PLAY_MOVIE (-orz-, MrPoke). Plays a cutscene/movie; sets WATCHING_CUTSCENE custom state. CustomPlayerState red-tolerated.</summary>
public class SM_PLAY_MOVIE : AionServerPacket
{
    private readonly bool isMovie;
    private readonly int objectId;
    private readonly int questId;
    private readonly int cutsceneId;
    private readonly bool canSkip;

    public SM_PLAY_MOVIE(bool isCutsceneMovie, int objectId, int questId, int cutsceneId, bool canSkip)
    {
        this.isMovie = isCutsceneMovie;
        this.objectId = objectId;
        this.questId = questId;
        this.cutsceneId = cutsceneId;
        this.canSkip = canSkip;
    }

    protected override void WriteImpl(AionConnection con)
    {
        con.GetActivePlayer().SetCustomState(CustomPlayerState.WATCHING_CUTSCENE);
        WriteC(isMovie ? 1 : 0); // if 1: CutSceneMovies else CutScenes
        WriteD(objectId);
        WriteD(questId);
        WriteD(cutsceneId);
        WriteC(0); // unknown
        WriteC(canSkip ? 0 : 1);
    }
}
