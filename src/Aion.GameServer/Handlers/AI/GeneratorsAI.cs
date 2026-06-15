using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/engulfedOphidianBridgeInstance/GeneratorsAI (Cheatkiller).</summary>
[AIName("engulfedophidiangens")]
public class GeneratorsAI : ActionItemNpcAI
{
    private Npc flag;

    public GeneratorsAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        switch (GetOwner().GetSpawn().GetStaticId())
        {
            case 160:
                flag = (Npc)Spawn(802038, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // NORTH
                PacketSendUtility.BroadcastToMap(GetOwner(), 1402001);
                break;
            case 164:
                flag = (Npc)Spawn(802035, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // GUARD
                PacketSendUtility.BroadcastToMap(GetOwner(), 1402000);
                break;
            case 158:
                flag = (Npc)Spawn(802041, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // SOUTHERN
                PacketSendUtility.BroadcastToMap(GetOwner(), 1402002);
                break;
            case 162:
                flag = (Npc)Spawn(802044, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // Defence
                PacketSendUtility.BroadcastToMap(GetOwner(), 1402003);
                break;
            case 175:
                flag = (Npc)Spawn(802036, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // NORTH
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401962);
                break;
            case 172:
                flag = (Npc)Spawn(802033, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // GUARD
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401961);
                break;
            case 169:
                flag = (Npc)Spawn(802039, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // SOUTHERN
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401963);
                break;
            case 171:
                flag = (Npc)Spawn(802042, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // Defence
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401964);
                break;
            case 174:
                flag = (Npc)Spawn(802037, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // NORTH
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401992);
                break;
            case 173:
                flag = (Npc)Spawn(802034, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // GUARD
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401991);
                break;
            case 166:
                flag = (Npc)Spawn(802040, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // SOUTHERN
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401993);
                break;
            case 170:
                flag = (Npc)Spawn(802043, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // Defence
                PacketSendUtility.BroadcastToMap(GetOwner(), 1401994);
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
        if (flag != null)
            flag.GetController().Delete();
    }
}
