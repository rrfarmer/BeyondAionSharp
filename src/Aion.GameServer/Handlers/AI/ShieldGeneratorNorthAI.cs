using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/illuminaryObelisk/ShieldGeneratorNorthAI (@author Estrayl).</summary>
[AIName("northern_generator")]
public class ShieldGeneratorNorthAI : ShieldGeneratorAI
{
    public ShieldGeneratorNorthAI(Npc owner)
        : base(owner)
    {
    }

    protected override SM_SYSTEM_MESSAGE GetAttackMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_DEFENCE_04_ATTACKED();
    }

    protected override SM_SYSTEM_MESSAGE GetChargeMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_CHARGE_04();
    }

    protected override SM_SYSTEM_MESSAGE GetDestructionMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_DESTROY_04();
    }

    protected override SM_SYSTEM_MESSAGE GetGateMsg()
    {
        return SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_N_WAVE_04_BEGIN();
    }

    protected override void HandleChargeComplete()
    {
        charges.Add((Npc)Spawn(702227 + chargeCount, 212.64922f, 254.5639f, 295.94763f, (sbyte)60));
    }

    protected override void HandleVortexSpawn()
    {
        Spawn(702017, 169.5563f, 254.52907f, 293.04276f, (sbyte)0, 17); // north
        Shout(SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_N_WAVE_04_BEGIN());
    }

    protected override void HandleVortexDespawn()
    {
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(702017))
        {
            npc.GetController().Delete();
        }
    }
}
