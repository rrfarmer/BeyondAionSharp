using System.Collections.Generic;
using System.Threading;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/KromedesTrialInstance (xTz, Gigi, Pad) : GeneralInstanceHandler. @InstanceID(300230000); AtomicBoolean isBossSpawned compareAndSet→int+Interlocked; sentMovies list; onEnterZone zoneName switch; onPlayMovieEnd; onDie; onReviveEvent; scheduleRespawn via SpawnEngine; isRestrictedToInstance switch-expr→switch. 1:1.</summary>
[InstanceID(300230000)]
public class KromedesTrialInstance : GeneralInstanceHandler
{
    private int isBossSpawned;
    private readonly List<int> sentMovies = new List<int>();
    private int skillId;
    private bool isPlayerInManor;

    public KromedesTrialInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnEnterInstance(Player player)
    {
        skillId = player.GetRace() == Race.ASMODIANS ? 19270 : 19220;
        SendMovie(player, 453);
        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(skillId, player, player);
    }

    public override void OnEnterZone(Player player, ZoneInstance zone)
    {
        switch (zone.GetAreaTemplate().GetZoneName().ToString())
        {
            case "MANOR_ENTRANCE_300230000":
                SendMovie(player, 462);
                break;
            case "KALIGA_LIBRARY_300230000":
                PacketSendUtility.SendMonologue(player, 1111370); // The door to the Kaliga Treasury should be around here somewhere....
                break;
            case "KALIGA_TREASURY_300230000":
                if (Interlocked.CompareExchange(ref isBossSpawned, 1, 0) == 0)
                {
                    Npc wyr = instance.GetNpc(217002);
                    Npc angerr = instance.GetNpc(217000);
                    Npc hamam = instance.GetNpc(216982);
                    if (IsDead(wyr) && IsDead(angerr) && IsDead(hamam))
                    {
                        Spawn(217005, 669.214f, 774.387f, 216.88f, (byte)60);
                        Spawn(217001, 663.8805f, 779.1967f, 216.26213f, (byte)60);
                        Spawn(217003, 663.0468f, 774.6116f, 216.26215f, (byte)60);
                        Spawn(217004, 663.0468f, 770.03815f, 216.26212f, (byte)60);
                    }
                    else
                    {
                        Spawn(217006, 669.214f, 774.387f, 216.88f, (byte)60);
                    }
                }
                break;
        }
    }

    public override void OnPlayMovieEnd(Player player, int movieId)
    {
        if (movieId == 454)
        {
            Npc magasPotion = instance.GetNpc(730308);
            if (magasPotion != null && PositionUtil.IsInRange(player, magasPotion, 20))
            {
                int relicKeyId = 185000109;
                player.GetInventory().DecreaseByItemId(relicKeyId, player.GetInventory().GetItemCountByItemId(relicKeyId));
                TeleportService.TeleportTo(player, instance, 687.56116f, 681.68225f, 200.28648f, (byte)30);
            }
        }
    }

    public override void OnDie(Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 216968: // Divine Hisen
                isPlayerInManor = true;
                break;
            case 282093: // Mana Relic
                RemoveKaligaBuff(19248);
                ScheduleRespawn(npc);
                npc.GetController().Delete();
                break;
            case 282095: // Strength Relic
                RemoveKaligaBuff(19247);
                ScheduleRespawn(npc);
                npc.GetController().Delete();
                break;
        }
    }

    public override bool OnReviveEvent(Player player)
    {
        PlayerReviveService.Revive(player, 25, 25, true, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        if (!isPlayerInManor)
            TeleportService.TeleportTo(player, instance, 248, 244, 189);
        else
            TeleportService.TeleportTo(player, instance, 686, 676, 201);
        Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(skillId, player, player);
        return true;
    }

    private void RemoveKaligaBuff(int skillId)
    {
        Npc kaliga = instance.GetNpc(217006);
        if (kaliga != null && !kaliga.IsDead())
        {
            kaliga.GetEffectController().RemoveEffect(skillId);
        }
    }

    private void ScheduleRespawn(Npc npc)
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            Npc kaliga = instance.GetNpc(217006);
            if (kaliga != null && !kaliga.IsDead())
            {
                SpawnTemplate npcST = npc.GetSpawn();
                SpawnTemplate newST = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(npcST.GetWorldId(), npc.GetNpcId(), npcST.GetX(), npcST.GetY(),
                    npcST.GetZ(), npcST.GetHeading());
                newST.SetStaticId(npcST.GetStaticId());
                Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(newST, instance.GetInstanceId());
            }
        }, 10000L);
    }

    private bool IsDead(Npc npc)
    {
        return npc == null || npc.IsDead();
    }

    private void SendMovie(Player player, int movieId)
    {
        if (!sentMovies.Contains(movieId))
        {
            sentMovies.Add(movieId);
            PacketSendUtility.SendPacket(player, new SM_PLAY_MOVIE(false, 0, 0, movieId, true));
        }
    }

    protected override bool IsRestrictedToInstance(Item item)
    {
        return item.GetItemId() switch
        {
            185000098 or 185000099 or 185000100 or 185000109 => true, // Temple Vault Door Key, Dungeon Grate Key, Dungeon Door Key, Relic Key
            _ => base.IsRestrictedToInstance(item),
        };
    }
}
