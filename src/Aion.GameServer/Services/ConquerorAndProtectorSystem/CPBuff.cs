using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Templates.Cp;

namespace Aion.GameServer.Services.ConquerorAndProtectorSystem;

/// <summary>Java parity: services/conquerorAndProtectorSystem/CPBuff. implements StatOwner→IStatOwner.</summary>
public class CPBuff : IStatOwner
{
    public void ApplyEffect(Player player, CPType type, int rank)
    {
        EndEffect(player);
        if (rank == 0)
            return;
        CPRank cpRank = DataManager.CONQUEROR_AND_PROTECTOR_DATA.GetRank(type, rank);
        if (cpRank != null && cpRank.GetStatModifiers().Count != 0)
            player.GetGameStats().AddEffect(this, cpRank.GetStatModifiers());
    }

    public void EndEffect(Player player)
    {
        player.GetGameStats().EndEffect(this);
    }
}
