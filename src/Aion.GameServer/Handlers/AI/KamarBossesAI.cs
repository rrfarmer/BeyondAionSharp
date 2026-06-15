using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Cheatkiller
/// </summary>
[AIName("kamarbosses")]
public class KamarBossesAI : AggressiveNpcAI
{
    private Npc flag;

    public KamarBossesAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        switch (GetOwner().GetNpcId())
        {
            case 232853:
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401845);
                break;
            case 232857:
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401848);
                break;
            case 232858:
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401850);
                break;
        }
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        switch (GetOwner().GetNpcId())
        {
            case 232854:
            case 232853:
            case 232852:
                flag = (Npc)Spawn(801956, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
                break;
            case 232857:
                flag = (Npc)Spawn(801957, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
                break;
            case 232858:
                flag = (Npc)Spawn(801958, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
                break;
        }
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        if (flag != null)
            flag.GetController().Delete();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        switch (GetOwner().GetNpcId())
        {
            case 232853:
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401846);
                break;
            case 232857:
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401849);
                break;
            case 232858:
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401851);
                break;
        }
        if (flag != null)
            flag.GetController().Delete();
    }
}
