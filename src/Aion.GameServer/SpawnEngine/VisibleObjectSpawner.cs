using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.GeoEngine.Collision;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Rift;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Pet;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Spawns.Riftspawns;
using Aion.GameServer.Model.Templates.Spawns.Siegespawns;
using Aion.GameServer.Model.Templates.Spawns.Vortexspawns;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Skillengine.Effects;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Idfactory;
using Aion.GameServer.World.Geo;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Spawnengine;

/// <summary>Java parity: spawnengine/VisibleObjectSpawner (ATracer). Object-spawn factory methods. slf4j parameterized log.error("...{}", x)→ILogger LogError("...{Name}", x); log.error(msg, ex)→LogError(ex, msg); Math.toRadians→*Math.PI/180; protected static→internal static (package access from SpawnEngine). Most controllers/models red-tolerated.</summary>
public class VisibleObjectSpawner
{
    private static readonly ILogger Log = NullLoggerFactory.Instance.CreateLogger(nameof(VisibleObjectSpawner));

    internal static VisibleObject SpawnNpc(SpawnTemplate spawn, int instanceIndex)
    {
        int npcId = spawn.GetNpcId();
        NpcTemplate npcTemplate = DataManager.NPC_DATA.GetNpcTemplate(npcId);
        if (npcTemplate == null)
        {
            Log.LogError("No template for NPC {NpcId}", npcId);
            return null;
        }
        Npc npc = new Npc(new NpcController(), spawn, npcTemplate);
        npc.SetCreatorId(spawn.GetCreatorId());
        npc.SetKnownlist(npc.IsFlag() ? new FlagKnownList(npc) : new NpcKnownList(npc));
        npc.SetEffectController(new EffectController(npc));

        if (WalkerFormator.ProcessClusteredNpc(npc, spawn.GetWorldId(), instanceIndex))
            return npc;

        try
        {
            SpawnEngine.BringIntoWorld(npc, spawn, instanceIndex);
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Error during spawn:");
            npc.GetController().Delete();
        }
        return npc;
    }

    public static SummonedHouseNpc SpawnHouseNpc(SpawnTemplate spawn, int instanceIndex, House creator)
    {
        SummonedHouseNpc npc = new SummonedHouseNpc(new NpcController(), spawn, creator);
        SpawnEngine.BringIntoWorld(npc, spawn, instanceIndex);
        return npc;
    }

    internal static VisibleObject SpawnRiftNpc(RiftSpawnTemplate spawn, int instanceIndex)
    {
        if (!CustomConfig.RIFT_ENABLED)
        {
            return null;
        }

        int npcId = spawn.GetNpcId();
        NpcTemplate npcTemplate = DataManager.NPC_DATA.GetNpcTemplate(npcId);
        if (npcTemplate == null)
        {
            Log.LogError("No template for NPC {NpcId}", npcId);
            return null;
        }
        Npc npc;

        int spawnId = spawn.GetId();
        RiftLocation loc = RiftService.GetInstance().GetRiftLocation(spawnId);
        if (loc.IsOpened() && spawnId == loc.GetId())
        {
            npc = new Npc(new NpcController(), spawn, npcTemplate);
            npc.SetKnownlist(new NpcKnownList(npc));
        }
        else
        {
            return null;
        }
        npc.SetEffectController(new EffectController(npc));
        SpawnEngine.BringIntoWorld(npc, spawn, instanceIndex);
        return npc;
    }

    internal static VisibleObject SpawnSiegeNpc(SiegeSpawnTemplate spawn, int instanceIndex)
    {
        if (!SiegeConfig.SIEGE_ENABLED)
            return null;

        NpcTemplate npcTemplate = DataManager.NPC_DATA.GetNpcTemplate(spawn.GetNpcId());
        if (npcTemplate == null)
        {
            Log.LogError("No template for NPC {NpcId}", spawn.GetNpcId());
            return null;
        }
        Npc npc = new SiegeNpc(new NpcController(), spawn, npcTemplate);
        npc.SetKnownlist(new NpcKnownList(npc));
        npc.SetEffectController(new EffectController(npc));
        SpawnEngine.BringIntoWorld(npc, spawn, instanceIndex);
        return npc;
    }

    internal static VisibleObject SpawnInvasionNpc(VortexSpawnTemplate spawn, int instanceIndex)
    {
        if (!CustomConfig.VORTEX_ENABLED)
        {
            return null;
        }

        NpcTemplate npcTemplate = DataManager.NPC_DATA.GetNpcTemplate(spawn.GetNpcId());
        if (npcTemplate == null)
        {
            Log.LogError("No template for NPC {NpcId}", spawn.GetNpcId());
            return null;
        }
        Npc npc = new Npc(new NpcController(), spawn, npcTemplate);
        npc.SetKnownlist(new NpcKnownList(npc));
        npc.SetEffectController(new EffectController(npc));
        SpawnEngine.BringIntoWorld(npc, spawn, instanceIndex);
        return npc;
    }

    public static Gatherable SpawnGatherable(SpawnTemplate spawn, int instanceIndex)
    {
        Gatherable gatherable = new Gatherable(spawn, new GatherableController());
        SpawnEngine.BringIntoWorld(gatherable, spawn, instanceIndex);
        return gatherable;
    }

    public static Trap SpawnTrap(SpawnTemplate spawn, int instanceIndex, Creature creator)
    {
        Trap trap = new Trap(new TrapController(), spawn, creator);
        SpawnEngine.BringIntoWorld(trap, spawn, instanceIndex);
        PacketSendUtility.BroadcastPacket(trap, new SM_PLAYER_STATE(trap));
        return trap;
    }

    public static GroupGate SpawnGroupGate(SpawnTemplate spawn, int instanceIndex, Creature creator)
    {
        GroupGate groupgate = new GroupGate(new NpcController(), spawn, creator);
        SpawnEngine.BringIntoWorld(groupgate, spawn, instanceIndex);
        return groupgate;
    }

    public static Kisk SpawnKisk(SpawnTemplate spawn, int instanceIndex, Player creator)
    {
        Kisk kisk = new Kisk(new NpcController(), spawn, creator);
        SpawnEngine.BringIntoWorld(kisk, spawn, instanceIndex);
        return kisk;
    }

    /// <summary>Spawns postman for express mail (ViAl)</summary>
    public static Npc SpawnPostman(Player owner)
    {
        int npcId = owner.GetRace() == Race.ELYOS ? 798100 : 798101;
        NpcTemplate template = DataManager.NPC_DATA.GetNpcTemplate(npcId);
        double radian = PositionUtil.ConvertHeadingToAngle(owner.GetHeading()) * Math.PI / 180;
        Vector3f pos = GeoService.GetInstance().GetClosestCollision(owner, owner.GetX() + (float)(Math.Cos(radian) * 7),
            owner.GetY() + (float)(Math.Sin(radian) * 7), owner.GetZ());
        SpawnTemplate spawn = SpawnEngine.NewSingleTimeSpawn(owner.GetWorldId(), npcId, pos.GetX(), pos.GetY(), pos.GetZ(), (byte)0);
        Npc postman = new Npc(new NpcController(), spawn, template);
        postman.SetCreatorId(owner.GetObjectId());
        postman.SetMasterName(owner.GetName());
        postman.SetKnownlist(new PlayerAwareKnownList(postman));
        postman.SetEffectController(new EffectController(postman));
        SpawnEngine.BringIntoWorld(postman, spawn, owner.GetInstanceId());
        owner.SetPostman(postman);
        return postman;
    }

    public static Npc SpawnFunctionalNpc(Player owner, int npcId, SummonOwner summonOwner)
    {
        NpcTemplate template = DataManager.NPC_DATA.GetNpcTemplate(npcId);
        double radian = PositionUtil.ConvertHeadingToAngle(owner.GetHeading()) * Math.PI / 180;
        Vector3f pos = GeoService.GetInstance().GetClosestCollision(owner, owner.GetX() + (float)(Math.Cos(radian) * 1),
            owner.GetY() + (float)(Math.Sin(radian) * 1), owner.GetZ());
        byte heading = PositionUtil.GetHeadingTowards(pos.GetX(), pos.GetY(), owner.GetX(), owner.GetY());
        SpawnTemplate spawn = SpawnEngine.NewSingleTimeSpawn(owner.GetWorldId(), npcId, pos.GetX(), pos.GetY(), pos.GetZ(), heading);
        Npc functionalNpc = new Npc(new NpcController(), spawn, template);
        functionalNpc.SetKnownlist(new PlayerAwareKnownList(functionalNpc));
        functionalNpc.SetEffectController(new EffectController(functionalNpc));
        functionalNpc.SetCreatorId(owner.GetObjectId());
        functionalNpc.SetSummonOwner(summonOwner);
        SpawnEngine.BringIntoWorld(functionalNpc, spawn, owner.GetInstanceId());
        return functionalNpc;
    }

    public static Servant SpawnServant(SpawnTemplate spawn, int instanceIndex, Creature creator, int level, NpcObjectType objectType)
    {
        Servant servant = new Servant(new NpcController(), spawn, creator.GetLevel(), creator);
        servant.SetNpcObjectType(objectType);
        servant.SetUpStats();
        SpawnEngine.BringIntoWorld(servant, spawn, instanceIndex);
        if (servant.GetSkillList() != null)
        {
            NpcSkillEntry skill = servant.GetSkillList().GetRandomSkill();
            if (skill != null)
            {
                SkillTemplate st = DataManager.SKILL_DATA.GetSkillTemplate(skill.GetSkillId());
                if (st.GetStartconditions() != null && st.GetHpCondition() != null)
                {
                    int hp = (st.GetHpCondition().GetHpValue() * 3);
                    servant.GetLifeStats().SetCurrentHp(hp);
                }
            }
        }
        return servant;
    }

    public static Servant SpawnEnemyServant(SpawnTemplate spawn, int instanceIndex, Creature creator, byte servantLvl)
    {
        Servant servant = new Servant(new NpcController(), spawn, servantLvl, creator);
        servant.SetNpcObjectType(NpcObjectType.SERVANT);
        SpawnEngine.BringIntoWorld(servant, spawn, instanceIndex);
        return servant;
    }

    public static Homing SpawnHoming(SpawnTemplate spawn, int instanceIndex, Creature creator, int attackCount, int skillId)
    {
        Homing homing = new Homing(new NpcController(), spawn, creator.GetLevel(), creator, skillId);
        homing.SetState(CreatureState.WEAPON_EQUIPPED);
        homing.SetAttackCount(attackCount);
        SpawnEngine.BringIntoWorld(homing, spawn, instanceIndex);
        return homing;
    }

    public static Summon SpawnSummon(Player creator, int npcId, int skillId, int time)
    {
        double radian = PositionUtil.ConvertHeadingToAngle(creator.GetHeading()) * Math.PI / 180;
        float x = creator.GetX() + (float)(Math.Cos(radian) * 2);
        float y = creator.GetY() + (float)(Math.Sin(radian) * 2);
        Vector3f pos = GeoService.GetInstance().GetClosestCollision(creator, x, y, creator.GetZ(), true, CollisionIntention.DEFAULT_COLLISIONS.GetId(), IgnoreProperties.Of(creator.GetRace()));
        byte heading = creator.GetHeading();
        int worldId = creator.GetWorldId();
        int instanceId = creator.GetInstanceId();

        SpawnTemplate spawn = SpawnEngine.NewSingleTimeSpawn(worldId, npcId, pos.GetX(), pos.GetY(), pos.GetZ(), heading);
        NpcTemplate npcTemplate = DataManager.NPC_DATA.GetNpcTemplate(npcId);

        bool isSiegeWeapon = "siege_weapon".Equals(npcTemplate.GetAiName());
        Summon summon = new Summon(IDFactory.GetInstance().NextId(), isSiegeWeapon ? new SiegeWeaponController(npcId) : new SummonController(), spawn,
            npcTemplate, creator, time);
        summon.SetKnownlist(new CreatureAwareKnownList(summon));
        summon.SetEffectController(new EffectController(summon));
        summon.GetLifeStats().SynchronizeWithMaxStats();
        summon.SetSummonedBySkillId(skillId);

        SpawnEngine.BringIntoWorld(summon, spawn, instanceId);
        if (isSiegeWeapon)
            summon.GetAi().OnGeneralEvent(AIEventType.SPAWNED);
        return summon;
    }

    public static Pet SpawnPet(Player player, int templateId)
    {
        PetCommonData petCommonData = player.GetPetList().GetPet(templateId);
        if (petCommonData == null)
            return null;

        PetTemplate petTemplate = DataManager.PET_DATA.GetPetTemplate(templateId);
        if (petTemplate == null)
            return null;

        Pet pet = new Pet(petTemplate, new PetController(), petCommonData, player);
        pet.SetKnownlist(new PlayerAwareKnownList(pet));

        float x = player.GetX();
        float y = player.GetY();
        float z = player.GetZ();
        byte heading = player.GetHeading();
        int worldId = player.GetWorldId();
        int instanceId = player.GetInstanceId();
        SpawnTemplate spawn = SpawnEngine.NewSingleTimeSpawn(worldId, templateId, x, y, z, heading);
        SpawnEngine.BringIntoWorld(pet, spawn, instanceId);
        player.SetPet(pet);
        return pet;
    }
}
