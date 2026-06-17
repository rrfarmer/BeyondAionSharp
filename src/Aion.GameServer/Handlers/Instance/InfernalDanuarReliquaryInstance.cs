using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/InfernalDanuarReliquaryInstance (Yeats) : DanuarReliquaryInstance. @InstanceID(301360000); overrides exit/treasure/modor/clone ids + onInstanceEnd (separate-from-base success/fail npc cleanup). 1:1.</summary>
[InstanceID(301360000)]
public class InfernalDanuarReliquaryInstance : DanuarReliquaryInstance
{
    public InfernalDanuarReliquaryInstance(WorldMapInstance instance) : base(instance)
    {
    }

    protected override int GetExitId()
    {
        return 730843;
    }

    protected override int GetTreasureBoxId()
    {
        return 701795;
    }

    protected override int GetEnragedModorId()
    {
        return 234691;
    }

    protected override int GetCursedModorId()
    {
        return 234690;
    }

    protected override int GetRealCloneId()
    {
        return 855244;
    }

    protected override int GetFakeCloneId()
    {
        return 855245;
    }

    protected override void OnInstanceEnd(bool successful)
    {
        CancelWipeTask();
        if (!successful)
        {
            Npc modor = GetNpc(GetEnragedModorId());
            if (modor == null)
            {
                modor = GetNpc(GetCursedModorId());
            }
            if (modor != null)
            {
                PacketSendUtility.BroadcastMessage(modor, 1500739);
            }
            instance.ForEachNpc(npc => npc.GetController().Delete());
        }
        else
        {
            Npc modor = GetNpc(GetEnragedModorId());
            if (modor != null)
            {
                PacketSendUtility.BroadcastMessage(modor, 343629);
            }
            instance.ForEachNpc(npc =>
            {
                if (npc.GetNpcId() != GetEnragedModorId())
                {
                    npc.GetController().Delete();
                }
            });
        }
        Spawn(GetExitId(), 255.66669f, 263.78525f, 241.7986f, (byte)86); // Spawn exit portal
    }
}
