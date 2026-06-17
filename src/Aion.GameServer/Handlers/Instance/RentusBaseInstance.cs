using System.Threading;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/RentusBaseInstance (xTz, Estrayl) : GeneralInstanceHandler. @InstanceID(300280000). AtomicBoolean→int+Interlocked. onDie boss webs + Vasharti end event walkers; onAggro doors; handleUseItemFinish teleports+applyEffectDirectly; onCreatureDetected Xasta event; onSpecialEvent; onEnterInstance race-known elyos swap; isBoss override. 1:1.</summary>
[InstanceID(300280000)]
public class RentusBaseInstance : GeneralInstanceHandler
{
    private int isRaceKnown;
    private int isXastaEventStarted;

    public RentusBaseInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        switch (npc.GetNpcId())
        {
            case 217315: // Umatha the Crazed
                if (IsDeadNpc(217316))
                {
                    instance.SetDoorState(145, true);
                    DeleteAliveNpcs(701156);
                }
                break;
            case 217316: // Ambusher Kiriana
                if (IsDeadNpc(217315))
                {
                    instance.SetDoorState(145, true);
                    DeleteAliveNpcs(701156);
                }
                break;
            case 217311: // Kuhara
                Spawn(219215, 141.54f, 255.06f, 213f, (byte)25);
                instance.SetDoorState(43, false);
                instance.SetDoorState(150, true);
                npc.GetController().Delete();
                break;
            case 217317: // Archmagus Upadi
                instance.SetDoorState(70, true);
                break;
            case 217313: // Brigade General Vasharti
                DeleteAliveNpcs(799669);
                instance.SetDoorState(70, true);
                Spawn(730520, 193.6f, 436.5f, 262f, (byte)86); // rentus base exit
                Spawn(833047, 195.48f, 413.87f, 260.97f, (byte)27); // rentus supplies storage box
                Npc ariana = (Npc)Spawn(799670, 183.736f, 391.392f, 260.571f, (byte)26);
                PacketSendUtility.BroadcastMessage(ariana, 1500417, 5000);
                PacketSendUtility.BroadcastMessage(ariana, 1500418, 8000);
                PacketSendUtility.BroadcastMessage(ariana, 1500419, 11000);
                SpawnEndEvent(800227, "3002800003", 2000);
                SpawnEndEvent(800227, "3002800004", 2000);
                SpawnEndEvent(800228, "3002800007", 4000);
                SpawnEndEvent(800227, "3002800005", 6000);
                SpawnEndEvent(800228, "3002800006", 8000);
                SpawnEndEvent(800229, "3002800008", 10000);
                SpawnEndEvent(800229, "3002800009", 10000);
                SpawnEndEvent(800230, "30028000010", 12000);
                SpawnEndEvent(800230, "30028000011", 12000);
                break;
            case 282394:
                Spawn(282395, npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading());
                npc.GetController().Delete();
                break;
            case 283000:
            case 283001:
                npc.GetController().Delete();
                break;
            case 236283:
                Spawn(236284, npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading());
                npc.GetController().Delete();
                break;
        }
    }

    public override void OnAggro(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 217311: // Kuhara
                instance.SetDoorState(43, true);
                PacketSendUtility.BroadcastMessage(npc, 1500393);
                break;
            case 217313: // Vasharti
                instance.SetDoorState(70, false);
                break;
        }
    }

    private void SpawnEndEvent(int npcId, string walkern, int time)
    {
        Sp(npcId, 193.39548f, 435.56158f, 260.57135f, (byte)86, time, walkern);
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 702677:
            case 702678:
            case 702679:
            case 702680:
            case 702681:
            case 702682:
                TeleportService.TeleportTo(player, npc.GetWorldId(), npc.GetInstanceId(), npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading());
                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21806, npc, player);
                npc.GetController().Delete();
                break;
            case 702683:
            case 702684:
            case 702685:
            case 702686:
            case 702687:
            case 702688:
                TeleportService.TeleportTo(player, npc.GetWorldId(), npc.GetInstanceId(), npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading());
                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21805, npc, player);
                npc.GetController().Delete();
                break;
            case 701097:
                npc.GetController().Delete();
                break;
            case 701100:
                if (instance.GetNpc(799543) == null)
                    Spawn(799543, 511.227f, 613.762f, 158.179f, (byte)0);
                break;
        }
    }

    private void Sp(int npcId, float x, float y, float z, byte h, int time, string walkern)
    {
        Npc npc = (Npc)Spawn(npcId, x, y, z, h);
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            npc.GetSpawn().SetWalkerId(walkern);
            WalkManager.StartWalking((NpcAI)npc.GetAi());
            npc.SetState(CreatureState.WALK_MODE, true);
            PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED, 0, npc.GetObjectId()));
        }, time);
    }

    private bool IsDeadNpc(int npcId)
    {
        return (GetNpc(npcId) == null || GetNpc(npcId).IsDead());
    }

    public override void OnCreatureDetected(Npc detector, Creature detected)
    {
        if (detected is Player)
        {
            if (detector.GetNpcId() == 856056 && Interlocked.CompareExchange(ref isXastaEventStarted, 1, 0) == 0)
            {
                Sp(236271, 521.33f, 499.49f, 179.946f, (byte)27, 2000, "300620000_Xasta_Path");
                detector.GetController().Delete();
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDYUN_RASTA_SPAWN_01());
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDYUN_RASTA_SPAWN_02(), 2000);
            }
        }
    }

    public override void OnSpecialEvent(Npc npc)
    {
        if (npc.GetNpcId() == 236271)
        {
            if (npc.GetLifeStats().GetHpPercentage() <= 50)
                Spawn(217310, 354.53f, 596.26f, 148.298f, (byte)100);
            else
                Spawn(217309, 354.53f, 596.26f, 148.298f, (byte)100);
            Npc drakanBarricade = GetNpc(856015);
            if (drakanBarricade != null)
                drakanBarricade.GetController().Die(npc);
            npc.GetController().Delete();
        }
    }

    public override void OnEnterInstance(Player player)
    {
        if (Interlocked.CompareExchange(ref isRaceKnown, 1, 0) == 0)
        {
            if (player.GetRace() == Race.ELYOS)
            {
                for (int npcId = 702677; npcId <= 702682; npcId++)
                {
                    Npc npc = GetNpc(npcId);
                    Spawn(npcId + 6, npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading(), npc.GetSpawn().GetStaticId());
                    npc.GetController().Delete();
                }
            }
        }
    }

    public override bool IsBoss(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 217309:
            case 217310:
            case 217311:
            case 217315:
            case 217316:
            case 217317:
            case 217313:
                return true;
            default:
                return false;
        }
    }
}
