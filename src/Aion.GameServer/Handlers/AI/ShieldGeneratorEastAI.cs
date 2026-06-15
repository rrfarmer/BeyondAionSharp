using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/illuminaryObelisk/ShieldGeneratorEastAI (@author Estrayl).</summary>
[AIName("eastern_generator")]
public class ShieldGeneratorEastAI : ShieldGeneratorAI
{
    public ShieldGeneratorEastAI(Npc owner)
        : base(owner)
    {
    }

    protected override SM_SYSTEM_MESSAGE GetAttackMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_DEFENCE_01_ATTACKED();
    }

    protected override SM_SYSTEM_MESSAGE GetChargeMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_CHARGE_01();
    }

    protected override SM_SYSTEM_MESSAGE GetDestructionMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_DESTROY_01();
    }

    protected override SM_SYSTEM_MESSAGE GetGateMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_N_WAVE_01_BEGIN();
    }

    protected override void HandleChargeComplete()
    {
        charges.Add((Npc)Spawn(702218 + chargeCount, 255.53876f, 297.46393f, 321.375f, (sbyte)30));
    }

    protected override void HandleVortexSpawn()
    {
        Spawn(702014, 255.7926f, 338.22058f, 325.56473f, (sbyte)0, 60); // east
        Shout(SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_N_WAVE_01_BEGIN());
    }

    protected override void HandleVortexDespawn()
    {
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(702014))
        {
            npc.GetController().Delete();
        }
    }
}
