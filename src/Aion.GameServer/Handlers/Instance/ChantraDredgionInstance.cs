using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Java parity: instance/dredgion/ChantraDredgionInstance (xTz) : DredgionInstance. 1:1.
/// </summary>
[InstanceID(300210000)]
public class ChantraDredgionInstance : DredgionInstance
{
    public ChantraDredgionInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnEnterInstance(Player player)
    {
        if (isInstanceStarted.CompareAndSet(false, true))
        {
            Sp(730311, 554.83081f, 173.87158f, 432.52448f, (byte)0, 9, 720000);
            Sp(730312, 397.11661f, 184.29782f, 432.80328f, (byte)0, 42, 720000);
            if (Rnd.Chance() < 21)
            {
                Sp(216889, 484.1199f, 314.08817f, 403.7213f, (byte)5, 720000);
            }
            if (Rnd.Chance() < 21)
            {
                Sp(216890, 499.52f, 598.67f, 390.49f, (byte)59, 720000);
            }
            if (Rnd.Chance() < 21)
            {
                Spawn(216887, 486.26382f, 909.48175f, 405.24463f, (byte)90);
            }
            if (Rnd.Chance() < 51)
            {
                switch (Rnd.NextInt(2))
                {
                    case 0:
                        Spawn(216888, 416.3429f, 282.32785f, 409.7311f, (byte)80);
                        break;
                    default:
                        Spawn(216888, 552.07446f, 289.058f, 409.7311f, (byte)80);
                        break;
                }
            }

            int spawnTime = Rnd.Get(10, 15) * 60 * 1000 + 120000;
            SendMsgByRace(1400633, Race.PC_ALL, spawnTime);
            Sp(216941, 485.99f, 299.23f, 402.57f, (byte)30, spawnTime);
            StartInstanceTask();
        }
        base.OnEnterInstance(player);
    }

    private void OnDieSurkana(Npc npc, Player mostPlayerDamage, int points)
    {
        Race race = mostPlayerDamage.GetRace();
        CaptureRoom(race, npc.GetNpcId() + 14 - 700851);
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_ROOM_DESTROYED(race.GetL10n(), npc.GetObjectTemplate().GetL10n()));
        GetPlayerReward(mostPlayerDamage).IncrementCapturedZones();
        if (killedSurkanas.IncrementAndGet() == 5)
        {
            Spawn(216886, 485.33f, 832.26f, 416.64f, (byte)55);
            SendMsgByRace(1400632, Race.PC_ALL, 0);
        }
        UpdateScore(mostPlayerDamage, npc, points, false);
        npc.GetController().Delete();
    }

    public override void OnDie(Npc npc)
    {
        if (instanceScore.GetInstanceProgressionType() != InstanceProgressionType.START_PROGRESS)
        {
            return;
        }
        switch (npc.GetNpcId())
        {
            case 730350: // Secondary Hatch teleporter
                SendMsgByRace(1400641, Race.PC_ALL, 0);
                Spawn(730315, 415.07663f, 173.85265f, 432.53436f, (byte)0, 34);
                npc.GetController().Delete();
                return;
            case 730349: // Escape Hatch teleporter
                SendMsgByRace(1400631, Race.PC_ALL, 0);
                Spawn(730314, 396.979f, 184.392f, 433.940f, (byte)0, 42);
                npc.GetController().Delete();
                return;
            case 730351:
                SendMsgByRace(1400226, Race.PC_ALL, 0);
                Spawn(730345, 448.391998f, 493.641998f, 394.131989f, (byte)90, 12);
                npc.GetController().Delete();
                return;
            case 730352:
                SendMsgByRace(1400227, Race.PC_ALL, 0);
                Spawn(730346, 520.875977f, 493.401001f, 394.433014f, (byte)90, 133);
                npc.GetController().Delete();
                return;
            case 216890:
            case 216889:
                return;
        }
        Player mostPlayerDamage = npc.GetAggroList().GetMostPlayerDamage();
        if (mostPlayerDamage == null)
        {
            return;
        }
        Race race = mostPlayerDamage.GetRace();
        switch (npc.GetNpcId())
        {
            case 700838:
            case 700839:
                OnDieSurkana(npc, mostPlayerDamage, 400);
                return;
            case 700840:
            case 700848:
            case 700849:
            case 700850:
            case 700851:
                OnDieSurkana(npc, mostPlayerDamage, 700);
                return;
            case 700845:
            case 700846:
                OnDieSurkana(npc, mostPlayerDamage, 800);
                return;
            case 700847:
                OnDieSurkana(npc, mostPlayerDamage, 900);
                return;
            case 700841:
            case 700842:
                OnDieSurkana(npc, mostPlayerDamage, 1000);
                return;
            case 700843:
            case 700844:
                OnDieSurkana(npc, mostPlayerDamage, 1100);
                return;
            case 216882: // Captain's Cabin teleport
                SendMsgByRace(1400652, Race.PC_ALL, 0);
                if (race == Race.ASMODIANS)
                {
                    Spawn(730358, 496.178f, 761.770f, 390.805f, (byte)0, 186);
                }
                else
                {
                    Spawn(730357, 473.759f, 761.864f, 390.805f, (byte)0, 33);
                }
                break;
            case 700836:
                UpdateScore(mostPlayerDamage, npc, 100, false);
                npc.GetController().Delete();
                return;
            case 216886:
                if (!instanceScore.IsRewarded())
                {
                    UpdateScore(mostPlayerDamage, npc, 1000, false);
                    StopInstance(instanceScore.GetRaceWithHighestPoints());
                }
                return;
            case 216941:
                UpdateScore(mostPlayerDamage, npc, 1000, false);
                return;
            case 216885:
                UpdateScore(mostPlayerDamage, npc, 500, false);
                return;
        }
        base.OnDie(npc);
    }

    protected override void OpenFirstDoors()
    {
        instance.SetDoorState(4, true);
        instance.SetDoorState(173, true);
    }
}
