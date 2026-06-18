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
/// Java parity: instance/dredgion/BaranathDredgionInstance (xTz) : DredgionInstance. 1:1.
/// </summary>
[InstanceID(300110000)]
public class BaranathDredgionInstance : DredgionInstance
{
    public BaranathDredgionInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnEnterInstance(Player player)
    {
        if (isInstanceStarted.CompareAndSet(false, true))
        {
            if (Rnd.Chance() < 51)
            {
                if (Rnd.NextBoolean())
                    Spawn(215093, 416.3429f, 282.32785f, 409.7311f, (byte)80);
                else
                    Spawn(215093, 552.07446f, 289.058f, 409.7311f, (byte)80);
            }
            StartInstanceTask();
        }
        base.OnEnterInstance(player);
    }

    private void OnDieSurkana(Npc npc, Player mostPlayerDamage, int points)
    {
        Race race = mostPlayerDamage.GetRace();
        CaptureRoom(race, npc.GetNpcId() + 14 - 700498);
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_ROOM_DESTROYED(race.GetL10n(), npc.GetObjectTemplate().GetL10n()));
        if (killedSurkanas.IncrementAndGet() == 5)
        {
            Spawn(214823, 485.423f, 808.826f, 416.868f, (byte)30);
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
            case 700485:
            case 700486:
            case 700495:
            case 700496:
                OnDieSurkana(npc, mostPlayerDamage, 500);
                return;
            case 700497:
            case 700498:
                OnDieSurkana(npc, mostPlayerDamage, 700);
                return;
            case 700493:
            case 700492:
                OnDieSurkana(npc, mostPlayerDamage, 800);
                return;
            case 700487:
                OnDieSurkana(npc, mostPlayerDamage, 900);
                return;
            case 700490:
            case 700491:
                OnDieSurkana(npc, mostPlayerDamage, 600);
                return;
            case 700488:
            case 700489:
                OnDieSurkana(npc, mostPlayerDamage, 1080);
                return;
            case 214823:
                UpdateScore(mostPlayerDamage, npc, 1000, false);
                StopInstance(race);
                if (race == Race.ELYOS)
                {
                    SendMsgByRace(1400230, Race.ELYOS, 0);
                }
                else
                {
                    SendMsgByRace(1400231, Race.ASMODIANS, 0);
                }
                return;
            case 700508:
                switch (race)
                {
                    case Race.ELYOS:
                        Spawn(700502, 520.88f, 493.40f, 395.34f, (byte)28, 16);
                        SendMsgByRace(1400226, Race.ELYOS, 0);
                        break;
                    case Race.ASMODIANS:
                        Spawn(700502, 448.39f, 493.64f, 395.04f, (byte)108, 12);
                        SendMsgByRace(1400227, Race.ASMODIANS, 0);
                        break;
                }
                return;
            case 700506:
                switch (race)
                {
                    case Race.ELYOS:
                        Spawn(730214, 567.59f, 175.20f, 432.28f, (byte)33);
                        SendMsgByRace(1400228, Race.ELYOS, 0);
                        break;
                    case Race.ASMODIANS:
                        Spawn(730214, 402.33f, 175.12f, 432.28f, (byte)41);
                        SendMsgByRace(1400229, Race.ASMODIANS, 0);
                        break;
                }
                return;
        }
        base.OnDie(npc);
    }

    public override void OnInstanceCreate()
    {
        InitializeInstance(3000, 1500, 2250);
    }

    protected override void OpenFirstDoors()
    {
        instance.SetDoorState(17, true);
        instance.SetDoorState(18, true);
    }
}
