using System.Collections.Generic;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/SauroSupplyBaseInstance (Cheatkiller) : GeneralInstanceHandler. @InstanceID(301130000). static chestPoints; Future→ScheduledTask. onInstanceCreate rnd grave robber/Ranodim/chest distribution; onDie door webs + Ahuradim generator task (Rnd.NextBoolean rotation, broadcastMessage, skill 20773/21200); onBackHome cancel; handleUseItemFinish teleports; isBoss override. 1:1.</summary>
[InstanceID(301130000)]
public class SauroSupplyBaseInstance : GeneralInstanceHandler
{
    private static readonly List<WorldPosition> chestPoints = new();
    private ScheduledTask scheduledGeneratorTask;

    static SauroSupplyBaseInstance()
    {
        chestPoints.Add(new WorldPosition(301130000, 253.97533f, 363.97156f, 159.64023f, (byte)0));
        chestPoints.Add(new WorldPosition(301130000, 262.36151f, 393.78619f, 156.83209f, (byte)30));
        chestPoints.Add(new WorldPosition(301130000, 263.41193f, 463.63086f, 156.70573f, (byte)90));
        chestPoints.Add(new WorldPosition(301130000, 282.64951f, 330.15793f, 159.36792f, (byte)0));
        chestPoints.Add(new WorldPosition(301130000, 300.37936f, 332.13354f, 159.74071f, (byte)60));
        chestPoints.Add(new WorldPosition(301130000, 325.39862f, 364.31143f, 160.89003f, (byte)60));
        chestPoints.Add(new WorldPosition(301130000, 325.41577f, 388.60355f, 160.89003f, (byte)60));
        chestPoints.Add(new WorldPosition(301130000, 431.13638f, 481.81009f, 183.08244f, (byte)0));
        chestPoints.Add(new WorldPosition(301130000, 465.58185f, 412.19705f, 183.75404f, (byte)90));
        chestPoints.Add(new WorldPosition(301130000, 467.54926f, 377.6048f, 182.8183f, (byte)30));
        chestPoints.Add(new WorldPosition(301130000, 495.8497f, 359.10245f, 182.77852f, (byte)30));
        chestPoints.Add(new WorldPosition(301130000, 496.02997f, 381.77118f, 182.94859f, (byte)30));
        chestPoints.Add(new WorldPosition(301130000, 497.55228f, 402.35077f, 183.24211f, (byte)30));
        chestPoints.Add(new WorldPosition(301130000, 510.22513f, 535.37701f, 182.3832f, (byte)90));
        chestPoints.Add(new WorldPosition(301130000, 518.31726f, 463.9472f, 182.00262f, (byte)45));
        chestPoints.Add(new WorldPosition(301130000, 519.60052f, 505.98093f, 182.13989f, (byte)60));
        chestPoints.Add(new WorldPosition(301130000, 569.55652f, 495.64389f, 193.63138f, (byte)0));
        chestPoints.Add(new WorldPosition(301130000, 576.3725f, 500.66254f, 202.54437f, (byte)105));
        chestPoints.Add(new WorldPosition(301130000, 576.96289f, 471.08911f, 202.54466f, (byte)15));
        chestPoints.Add(new WorldPosition(301130000, 599.65344f, 361.96112f, 204.74123f, (byte)0));
        chestPoints.Add(new WorldPosition(301130000, 656.28381f, 398.28476f, 204.74123f, (byte)90));
        chestPoints.Add(new WorldPosition(301130000, 666.01855f, 363.71048f, 204.74123f, (byte)90));
        chestPoints.Add(new WorldPosition(301130000, 666.21979f, 345.6026f, 204.63773f, (byte)30));
    }

    public SauroSupplyBaseInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        // spawn Sauro Base Grave Robber (pool=1)
        switch (Rnd.Get(1, 5))
        {
            case 1: Spawn(230846, 460.69705f, 390.10602f, 182.75943f, (byte)0); break;
            case 2: Spawn(230846, 463.92523f, 402.63937f, 183.31064f, (byte)0); break;
            case 3: Spawn(230846, 496.42526f, 412.04755f, 182.72771f, (byte)0); break;
            case 4: Spawn(230846, 497.27997f, 361.05948f, 182.45613f, (byte)0); break;
            case 5: Spawn(230846, 497.55908f, 389.86649f, 182.8175f, (byte)0); break;
        }

        // spawn Commander Ranodim
        Spawn(Rnd.Get(new int[] { 230852, 233316, 233317 }), 289.8795f, 343.75858f, 159.34445f, (byte)90);

        List<WorldPosition> temp = new(chestPoints);
        for (int i = 0; i < 8; i++)
        {
            int index = Rnd.NextInt(temp.Count);
            WorldPosition pos = temp[index];
            temp.RemoveAt(index);
            Spawn(230847, pos.GetX(), pos.GetY(), pos.GetZ(), pos.GetHeading());
        }
        foreach (WorldPosition worldPosition in temp)
        {
            Spawn(230848, worldPosition.GetX(), worldPosition.GetY(), worldPosition.GetZ(), worldPosition.GetHeading());
        }
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        switch (npc.GetNpcId())
        {
            case 230837:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDVritra_Base_DoorOpen_03());
                instance.SetDoorState(372, true);
                break;
            case 230849:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDVritra_Base_DoorOpen_01());
                instance.SetDoorState(383, true);
                break;
            case 230850:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDVritra_Base_DoorOpen_04());
                instance.SetDoorState(375, true);
                break;
            case 230851:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDVritra_Base_DoorOpen_02());
                instance.SetDoorState(59, true);
                break;
            case 230852: // Ranodim
            case 233316:
            case 233317:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDVritra_Base_DoorOpen_06());
                instance.SetDoorState(388, true);
                break;
            case 233255:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDVritra_Base_DoorOpen_05());
                instance.SetDoorState(378, true);
                break;
            case 230838:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDVritra_Base_DoorOpen_07());
                instance.SetDoorState(376, true);
                break;
            case 230853:
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDVritra_Base_DoorOpen_08());
                Spawn(730872, 129.16417f, 432.32669f, 153.33147f, (byte)2, 3); // Boss Teleporter
                break;
            case 230854:
                Spawn(801967, 91.69f, 889.8f, 411.45f, (byte)120); // Sauro Exit
                break;
            case 230855:
                Spawn(801967, 289.38f, 889.82f, 411.45f, (byte)120); // Sauro Exit
                break;
            case 230856:
                Spawn(801967, 485.1f, 889.93f, 411.45f, (byte)0); // Sauro Exit
                break;
            case 230857:
                Spawn(801967, 721.84f, 889.93f, 411.45f, (byte)60); // Sauro Exit
                CancelTask();
                break;
            case 230858:
                Spawn(801967, 880.82f, 889.93f, 411.45f, (byte)120); // Sauro Exit
                break;
            case 284437: // protective shield generator
            case 284445:
            case 284446:
                if (instance.GetNpc(230857) != null && !instance.GetNpc(230857).IsDead())
                {
                    Spawn(npc.GetNpcId(), npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading());
                    Npc ahuradim = instance.GetNpc(230857);
                    if (ahuradim != null && !ahuradim.IsDead())
                    {
                        if (npc.GetNpcId() == 284437)
                        {
                            StartGeneratorTask(Rnd.NextBoolean() ? 284445 : 284446, ahuradim);
                        }
                        else if (npc.GetNpcId() == 284445)
                        {
                            StartGeneratorTask(Rnd.NextBoolean() ? 284446 : 284437, ahuradim);
                        }
                        else
                        {
                            StartGeneratorTask(Rnd.NextBoolean() ? 284437 : 284445, ahuradim);
                        }
                    }
                }
                npc.GetController().Delete();
                ThreadPoolManager.GetInstance().Schedule(() =>
                {
                    Npc gen1 = instance.GetNpc(284437);
                    Npc gen2 = instance.GetNpc(284445);
                    Npc gen3 = instance.GetNpc(284446);
                    if (gen1 != null)
                    {
                        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(gen1, 20773, 1, gen1).UseWithoutPropSkill();
                    }
                    if (gen2 != null)
                    {
                        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(gen2, 20773, 1, gen2).UseWithoutPropSkill();
                    }
                    if (gen3 != null)
                    {
                        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(gen3, 20773, 1, gen3).UseWithoutPropSkill();
                    }
                }, 500L);
                break;
        }
    }

    private void StartGeneratorTask(int npcId, Npc ahuradim)
    {
        scheduledGeneratorTask = ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (ahuradim.IsDead())
                return;
            Npc generator = instance.GetNpc(npcId);
            if (generator == null || generator.IsDead())
                return;
            generator.SetTarget(ahuradim);
            PacketSendUtility.BroadcastMessage(generator, 1501014);
            scheduledGeneratorTask = ThreadPoolManager.GetInstance().Schedule(() =>
            {
                if (!generator.IsDead())
                {
                    PacketSendUtility.BroadcastMessage(generator, 1501015);
                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(generator, 21200, 1, generator).UseWithoutPropSkill();
                }
            }, 15 * 1000L);
        }, 40 * 1000L);
    }

    private void CancelTask()
    {
        if (scheduledGeneratorTask != null)
        {
            scheduledGeneratorTask.Cancel(false);
        }
    }

    public override void OnBackHome(Npc npc)
    {
        if (npc.GetNpcId() == 230857)
        {
            CancelTask();
        }
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 730876:
                TeleportService.TeleportTo(player, player.GetWorldId(), player.GetInstanceId(), 721.84f, 889.93f, 411.45f, (byte)60,
                    TeleportAnimation.FADE_OUT_BEAM);
                break;
            case 730877:
                TeleportService.TeleportTo(player, player.GetWorldId(), player.GetInstanceId(), 880.82f, 889.93f, 411.45f, (byte)120,
                    TeleportAnimation.FADE_OUT_BEAM);
                break;
        }
    }

    public override bool IsBoss(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 230849:
            case 230850:
            case 230851:
            case 230853:
            case 230857:
            case 230858:
            case 233255:
            case 233256:
            case 233257:
            case 233258:
                return true;
            default:
                return false;
        }
    }
}
