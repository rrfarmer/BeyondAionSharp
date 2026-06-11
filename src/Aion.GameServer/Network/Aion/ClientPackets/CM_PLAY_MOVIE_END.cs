using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_PLAY_MOVIE_END (MrPoke). Returns SM_PLAY_MOVIE data after a cutscene finishes/skips; validates server-initiated cutscenes (book auto-movies allowed). QuestEngine red-tolerated.</summary>
public class CM_PLAY_MOVIE_END : AionClientPacket
{
    private byte type;
    private int targetObjectId;
    private int questId;
    private int movieId;
    private bool canSkip;

    public CM_PLAY_MOVIE_END(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        type = ReadC(); // 1: CutSceneMovies, otherwise CutScenes
        targetObjectId = ReadD();
        questId = ReadD();
        movieId = ReadD();
        ReadC(); // unknown
        canSkip = ReadC() == 0;
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (!player.IsInCustomState(CustomPlayerState.WATCHING_CUTSCENE))
        {
            // the client automatically plays movies when reading certain books (3: 730079/730091, 4: 730092, 5: 730085)
            ISet<int> bookMovieIds = type == 1 ? new HashSet<int> { 3, 4, 5 } : new HashSet<int>();
            if (questId != 0 || !bookMovieIds.Contains(movieId))
                AuditLogger.Log(player, "sent " + GetPacketName() + " for cutscene " + movieId + " that wasn't sent by the server");
            return;
        }
        player.UnsetCustomState(CustomPlayerState.WATCHING_CUTSCENE);
        VisibleObject target = player.IsTargeting(targetObjectId) ? player.GetTarget() : null;
        QuestEngine.GetInstance().OnMovieEnd(new QuestEnv(target, player, questId), movieId);
        player.GetPosition().GetWorldMapInstance().GetInstanceHandler().OnPlayMovieEnd(player, movieId);
    }
}
