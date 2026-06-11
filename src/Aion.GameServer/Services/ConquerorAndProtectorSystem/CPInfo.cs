using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Cp;

namespace Aion.GameServer.Services.ConquerorAndProtectorSystem;

/// <summary>Java parity: services/conquerorAndProtectorSystem/CPInfo (Source). Per-player conqueror/protector state: type, rank, legion-dominion rank, victim count, and CPBuff. getType()->GetType_() (Object.GetType collision). CPType red-tolerated.</summary>
public class CPInfo
{
    private readonly CPType type;
    private readonly int playerId;
    private int rank;
    private int ldRank;
    private int victims;
    private CPBuff buff;

    public CPInfo(CPType type, Player owner)
    {
        this.type = type;
        playerId = owner.GetObjectId();
        buff = new CPBuff();
    }

    public CPType GetType_()
    {
        return type;
    }

    public int GetPlayerId()
    {
        return playerId;
    }

    public void SetRank(int rank)
    {
        this.rank = rank;
    }

    public void SetLDRank(int rank)
    {
        ldRank = rank;
    }

    public int GetRank()
    {
        return rank;
    }

    public int GetLDRank()
    {
        return ldRank;
    }

    public int GetVictims()
    {
        return victims;
    }

    public void SetVictims(int victims)
    {
        this.victims = victims;
    }

    public CPBuff GetBuff()
    {
        return buff;
    }
}
