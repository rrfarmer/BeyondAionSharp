using System.Collections.Generic;
using System.Threading;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Flyring;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Model.Templates.Flyring;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Summons;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/TalocsHollowInstance (xTz, Skyra) : GeneralInstanceHandler. @InstanceID(300190000). AtomicBoolean isQueenMosquaHome→int (compareAndSet via instance, single-threaded boss callbacks → keep bool+lock-free); movies list; onEnterInstance adds quest items; isRestrictedToInstance override; onAggro/onBackHome queen door; onDie boss egg/fragment webs + summon release movie; handleUseItemFinish heal; spawnRings (FlyRing) + onPassFlyingRing movies; onReviveEvent. 1:1.</summary>
[InstanceID(300190000)]
public class TalocsHollowInstance : GeneralInstanceHandler
{
    private readonly List<int> movies = new();
    private int isQueenMosquaHome = 1;

    public TalocsHollowInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnEnterInstance(Player player)
    {
        AddItems(player);
    }

    private void AddItems(Player player)
    {
        QuestState qs1 = player.GetQuestStateList().GetQuestState(10032);
        QuestState qs2 = player.GetQuestStateList().GetQuestState(20032);
        if ((qs1 != null && qs1.GetStatus() == QuestStatus.START) || (qs2 != null && qs2.GetStatus() == QuestStatus.START))
            return;
        AddMissingItems(player, 164000099); // Taloc's Tears
        AddMissingItems(player, player.GetRace() == Race.ELYOS ? 160001286 : 160001287); // Taloc Fruit
    }

    private void AddMissingItems(Player player, int itemId)
    {
        if (player.GetInventory().GetFirstItemByItemId(itemId) == null)
            ItemService.AddItem(player, itemId, 1);
    }

    protected override bool IsRestrictedToInstance(Item item)
    {
        switch (item.GetItemId())
        {
            case 164000099: // Taloc's Tears
            case 164000137: // Shishir's Powerstone
            case 164000138: // Gellmar's Wardstone
            case 164000139: // Neith's Sleepstone
            case 185000088: // Shishir's Corrosive Fluid
            case 185000108: // Dorkin's Pocket Knife
                return true;
        }
        return base.IsRestrictedToInstance(item);
    }

    public override void OnAggro(Npc npc)
    {
        if (npc.GetNpcId() == 215480 && Interlocked.CompareExchange(ref isQueenMosquaHome, 0, 1) == 1)
            instance.SetDoorState(7, false);
    }

    public override void OnBackHome(Npc npc)
    {
        if (npc.GetNpcId() == 215480) // queen mosqua
        {
            Interlocked.Exchange(ref isQueenMosquaHome, 1);
            instance.SetDoorState(7, true);
        }
    }

    public override void OnDie(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 215467: // kinquid
                instance.SetDoorState(48, true);
                instance.SetDoorState(49, true);
                break;
            case 215457: // ancient octanus
                DeleteAliveNpcs(700633);
                break;
            case 215480: // queen mosqua
                instance.SetDoorState(7, true);
                Npc insectEgg = GetNpc(700738);
                if (insectEgg != null)
                {
                    insectEgg.GetController().Delete();
                    SpawnTemplate eggTemplate = insectEgg.GetSpawn();
                    Spawn(700739, eggTemplate.GetX(), eggTemplate.GetY(), eggTemplate.GetZ(), eggTemplate.GetHeading(), 11);
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDELIM_EGG_BREAK());
                    instance.ForEachPlayer(player =>
                    {
                        Summon summon = player.GetSummon();
                        if (summon != null)
                        {
                            if (summon.GetNpcId() == 799500 || summon.GetNpcId() == 799501)
                            {
                                SummonsService.DoMode(SummonMode.RELEASE, summon, UnsummonType.UNSPECIFIED);
                                PacketSendUtility.SendPacket(player, new SM_PLAY_MOVIE(false, 0, 0, 435, true));
                            }
                        }
                    });
                }
                break;
            case 700739: // cracked huge insect egg
                SpawnTemplate crackedEggTemplate = npc.GetSpawn();
                Spawn(281817, crackedEggTemplate.GetX(), crackedEggTemplate.GetY(), crackedEggTemplate.GetZ(), crackedEggTemplate.GetHeading(), 9);
                SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_IDELIM_WIND_INFO());
                break;
            case 215488: // celestius
                Player damagePlayer = npc.GetAggroList().GetMostPlayerDamage();
                if (damagePlayer != null)
                    PacketSendUtility.SendPacket(damagePlayer, new SM_PLAY_MOVIE(false, 0, 10021, 437, true));
                Npc contaminatedFragment = GetNpc(700740);
                if (contaminatedFragment != null)
                {
                    SpawnTemplate fragmentTemplate = contaminatedFragment.GetSpawn();
                    Spawn(700741, fragmentTemplate.GetX(), fragmentTemplate.GetY(), fragmentTemplate.GetZ(), fragmentTemplate.GetHeading(), 92);
                    contaminatedFragment.GetController().Delete();
                }
                Spawn(799503, 548f, 811f, 1375f, (byte)0);
                break;
        }
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 700940:
                player.GetLifeStats().IncreaseHp(TYPE.HP, 20000, npc);
                npc.GetController().Delete();
                break;
            case 700941:
                player.GetLifeStats().IncreaseHp(TYPE.HP, 30000, npc);
                npc.GetController().Delete();
                break;
        }
    }

    private void SendMovie(Player player, int movie)
    {
        if (!movies.Contains(movie))
        {
            movies.Add(movie);
            PacketSendUtility.SendPacket(player, new SM_PLAY_MOVIE(false, 0, 0, movie, true));
        }
    }

    public override void OnInstanceCreate()
    {
        instance.SetDoorState(48, true);
        instance.SetDoorState(7, true);
        SpawnRings();
    }

    private void SpawnRings()
    {
        FlyRing f1 = new FlyRing(new FlyRingTemplate("TALOCS_1", mapId, new Point3D(253.85039, 649.23535, 1171.8772),
            new Point3D(253.85039, 649.23535, 1177.8772), new Point3D(262.84872, 649.4091, 1171.8772), 8), instance.GetInstanceId());
        f1.Spawn();
        FlyRing f2 = new FlyRing(new FlyRingTemplate("TALOCS_2", mapId, new Point3D(592.32275, 844.056, 1295.0966),
            new Point3D(592.32275, 844.056, 1301.0966), new Point3D(595.2305, 835.5387, 1295.0966), 8), instance.GetInstanceId());
        f2.Spawn();
    }

    public override bool OnPassFlyingRing(Player player, string flyingRing)
    {
        if (flyingRing.Equals("TALOCS_1"))
        {
            SendMovie(player, 463);
        }
        else if (flyingRing.Equals("TALOCS_2"))
        {
            SendMovie(player, 464);
        }
        return false;
    }

    public override bool OnReviveEvent(Player player)
    {
        PlayerReviveService.Revive(player, 25, 25, false, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        TeleportService.TeleportTo(player, instance, 202.26694f, 226.0532f, 1098.236f, (byte)30);
        return true;
    }
}
