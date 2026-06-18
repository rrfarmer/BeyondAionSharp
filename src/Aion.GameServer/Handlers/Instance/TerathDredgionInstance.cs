using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Java parity: instance/dredgion/TerathDredgionInstance (xTz) : DredgionInstance. 1:1.
/// </summary>
[InstanceID(300440000)]
public class TerathDredgionInstance : DredgionInstance
{
    public TerathDredgionInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnEnterInstance(Player player)
    {
        if (isInstanceStarted.CompareAndSet(false, true))
        {
            Sp(730558, 415.034f, 174.004f, 433.940f, (byte)0, 34, 720000);
            Sp(730559, 572.038f, 185.252f, 433.940f, (byte)0, 10, 720000);
            SendMsgByRace(1401424, Race.PC_ALL, 720000);
            if (Rnd.Chance() < 21)
            {
                Sp(233372, 476.63f, 312.16f, 402.89807f, (byte)97, 720000, "5540A84BAD08498B96C315281F6418D0BD825175");
            }
            if (Rnd.Chance() < 21)
            {
                Sp(233373, 485.403f, 596.602f, 390.944f, (byte)90, 720000);
            }
            if (Rnd.Chance() < 21)
            {
                Sp(233379, 486.26382f, 906.011f, 405.24463f, (byte)90, 720000);
            }
            if (Rnd.Chance() < 51)
            {
                switch (Rnd.NextInt(2)) // Supervisor Chitan
                {
                    case 0:
                        Spawn(233362, 421.89111f, 285.20471f, 409.7311f, (byte)80);
                        break;
                    default:
                        Spawn(233362, 551.407f, 289.058f, 409.7311f, (byte)80);
                        break;
                }
            }
            int spawnTime = Rnd.Get(10, 15) * 60 * 1000 + 120000;
            SendMsgByRace(1401417, Race.PC_ALL, spawnTime);
            Sp(233377, 484.664f, 314.207f, 403.715f, (byte)30, spawnTime); // Enforcer Udara
            StartInstanceTask();
        }
        base.OnEnterInstance(player);
    }

    private void OnDieSurkana(Npc npc, Player mostPlayerDamage, int points)
    {
        Race race = mostPlayerDamage.GetRace();
        CaptureRoom(race, npc.GetNpcId() + 14 - 701454);
        foreach (Player player in instance.GetPlayersInside())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_ROOM_DESTROYED(race.GetL10n(), npc.GetObjectTemplate().GetL10n()));
        }
        if (killedSurkanas.IncrementAndGet() == 5)
        {
            Spawn(233371, 485.423f, 808.826f, 416.868f, (byte)30);
            SendMsgByRace(1401416, Race.PC_ALL, 0);
        }
        GetPlayerReward(mostPlayerDamage).IncrementCapturedZones();
        UpdateScore(mostPlayerDamage, npc, points, false);
        npc.GetController().Delete();
    }

    public override void OnDie(Npc npc)
    {
        Player mostPlayerDamage = npc.GetAggroList().GetMostPlayerDamage();
        if (mostPlayerDamage == null || instanceScore.GetInstanceProgressionType() != InstanceProgressionType.START_PROGRESS)
        {
            return;
        }
        Race race = mostPlayerDamage.GetRace();
        switch (npc.GetNpcId())
        {
            case 701441:
            case 701442:
                OnDieSurkana(npc, mostPlayerDamage, 400);
                return;
            case 701443:
            case 701451:
            case 701452:
            case 701453:
            case 701454:
                OnDieSurkana(npc, mostPlayerDamage, 700);
                return;
            case 701448:
            case 701449:
                OnDieSurkana(npc, mostPlayerDamage, 800);
                return;
            case 701450:
                OnDieSurkana(npc, mostPlayerDamage, 900);
                return;
            case 701444:
            case 701445:
                OnDieSurkana(npc, mostPlayerDamage, 1000);
                return;
            case 701446:
            case 701447:
                OnDieSurkana(npc, mostPlayerDamage, 1100);
                return;
            case 730572:
                Spawn(730566, 446.729f, 493.224f, 395.938f, (byte)0, 12);
                npc.GetController().Delete();
                return;
            case 730573:
                Spawn(730567, 520.404f, 493.261f, 395.938f, (byte)0, 133);
                npc.GetController().Delete();
                return;
            case 730570:
                SendMsgByRace(1401418, race, 0);
                Spawn(730560, 396.979f, 184.392f, 433.940f, (byte)0, 42);
                break;
            case 730571:
                SendMsgByRace(1401418, race, 0);
                Spawn(730561, 554.64f, 173.535f, 433.940f, (byte)0, 9);
                break;
            case 233378: // Master at Arms Vandukar
                SendMsgByRace(1401419, Race.PC_ALL, 0);
                if (race == Race.ASMODIANS)
                {
                    Spawn(730563, 496.178f, 761.770f, 390.805f, (byte)0, 186);
                }
                else
                {
                    Spawn(730562, 473.759f, 761.864f, 390.805f, (byte)0, 33);
                }
                return;
            case 233377: // Enforcer Udara
                UpdateScore(mostPlayerDamage, npc, 1000, false);
                if (Rnd.NextBoolean())
                {
                    Spawn(701455, 484.500f, 495.700f, 397.425f, (byte)33);
                    SendMsgByRace(1401421, Race.PC_ALL, 0);
                }
                return;
            case 233370:
                UpdateScore(mostPlayerDamage, npc, 500, false);
                return;
            case 233371: // Captain Anusa
                if (!instanceScore.IsRewarded())
                {
                    UpdateScore(mostPlayerDamage, npc, 1000, false);
                    StopInstance(instanceScore.GetRaceWithHighestPoints());
                }
                return;
            case 701439:
                UpdateScore(mostPlayerDamage, npc, 100, false);
                npc.GetController().Delete();
                return;
        }
        base.OnDie(npc);
    }

    public override void DoReward(Player player, PvpInstancePlayerReward reward, Race winningRace)
    {
        if (reward.GetRace() == winningRace)
        {
            reward.SetReward1(186000242, 1, 0); // CUSTOM: Ceramium Medal
            if (Rnd.Chance() < 20)
                reward.SetReward2(188950017, 1, 0); // CUSTOM: Special Courier Pass (Abyss Eternal/Lv. 61-65)
        }
        else
        {
            reward.SetReward1(186000147, 1, 0); // CUSTOM: Mithril Medal
        }
        base.DoReward(player, reward, winningRace);
    }

    protected override void OpenFirstDoors()
    {
        instance.SetDoorState(4, true);
        instance.SetDoorState(173, true);
    }
}
