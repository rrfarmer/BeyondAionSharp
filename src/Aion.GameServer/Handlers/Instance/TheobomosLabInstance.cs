using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/TheobomosLabInstance (xTz, Ritsu, Tibald) : GeneralInstanceHandler. @InstanceID(310110000); getMaster() instanceof Player→is Player; PositionUtil.getDistance≤7; ThreadPoolManager.schedule(...,1000); commented onEnterInstance preserved. 1:1.</summary>
[InstanceID(310110000)]
public class TheobomosLabInstance : GeneralInstanceHandler
{
    private bool isInstanceDestroyed;
    private bool isDead1 = false;
    private bool isDead2 = false;

    public TheobomosLabInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnDie(Npc npc)
    {
        if (isInstanceDestroyed)
            return;

        if (npc.GetMaster() is Player)
            return;

        switch (npc.GetNpcId())
        {
            case 214669: // Triroan
                Spawn(730178, 571.15f, 490.6607f, 196.7324f, (byte)60);
                break;
            case 280971:
            case 280972:
                if (GuardDie(npc))
                    RemoveBuff();
                break;
        }
    }

    /**
     * @Override public void onEnterInstance(Player player) { final QuestState qs = player.getQuestStateList().getQuestState(1094); if (qs != null &&
     *           qs.getStatus() == QuestStatus.COMPLETE) doors.get(37).setOpen(true); else doors.get(37).setOpen(false); }//this door is static door, so
     *           we cant control it.
     */
    public override void OnInstanceDestroy()
    {
        isDead1 = false;
        isDead2 = false;
        isInstanceDestroyed = true;
    }

    private bool GuardDie(Npc npc)
    {
        WorldPosition p = npc.GetPosition();
        int npcId = npc.GetNpcId();
        Npc orb = GetNpc(280973);
        if (orb != null && PositionUtil.GetDistance(orb, npc) <= 7)
        {
            switch (npcId)
            {
                case 280971:
                    isDead1 = true;
                    break;
                case 280972:
                    isDead2 = true;
                    break;
            }
            return true;
        }
        else
        {
            npc.GetController().Delete();
            Spawn(npcId, p.GetX(), p.GetY(), p.GetZ(), (byte)41);
            return false;
        }
    }

    private void RemoveBuff()
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!isInstanceDestroyed && isDead1 && isDead2)
            {
                GetNpc(214668).GetEffectController().RemoveEffect(18481);
                DeleteAliveNpcs(280973);
            }
        }, 1000L);
    }

    public override void OnInstanceCreate()
    {
        if (Rnd.NextBoolean())
        {
            switch (Rnd.Get(1, 3))
            {
                case 1:
                    Spawn(798223, 256.26215f, 512.9742f, 187.79453f, (byte)0);
                    break;
                case 2:
                    Spawn(798223, 360.16098f, 526.9446f, 186.20251f, (byte)105);
                    break;
                case 3:
                    Spawn(798223, 476.77145f, 541.5697f, 187.79453f, (byte)90);
                    break;
            }
        }
    }
}
