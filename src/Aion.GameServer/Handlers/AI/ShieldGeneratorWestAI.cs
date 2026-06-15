using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/illuminaryObelisk/ShieldGeneratorWestAI (@author Estrayl).</summary>
[AIName("western_generator")]
public class ShieldGeneratorWestAI : ShieldGeneratorAI
{
    public ShieldGeneratorWestAI(Npc owner)
        : base(owner)
    {
    }

    protected override SM_SYSTEM_MESSAGE GetAttackMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_DEFENCE_02_ATTACKED();
    }

    protected override SM_SYSTEM_MESSAGE GetChargeMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_CHARGE_02();
    }

    protected override SM_SYSTEM_MESSAGE GetDestructionMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_DESTROY_02();
    }

    protected override SM_SYSTEM_MESSAGE GetGateMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_N_WAVE_02_BEGIN();
    }

    protected override void HandleChargeComplete()
    {
        charges.Add((Npc)Spawn(702221 + chargeCount, 255.38824f, 211.9726f, 321.37753f, (sbyte)90));
    }

    protected override void HandleVortexSpawn()
    {
        Spawn(702015, 255.7034f, 171.83853f, 325.81653f, (sbyte)0, 18); // west
        Shout(SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_N_WAVE_01_BEGIN());
    }

    protected override void HandleVortexDespawn()
    {
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(702015))
        {
            npc.GetController().Delete();
        }
    }
}
