using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.Autogroup;

/// <summary>Java parity: model/autogroup/AutoPvPFFAInstance (xTz) : AutoInstance. synchronized→lock(this); Map.putAll→foreach add; isEmpty→Count==0. PvPArenaScore/AutoGroupService/SM_AUTO_GROUP red-tolerated.</summary>
public class AutoPvPFFAInstance : AutoInstance
{
    public AutoPvPFFAInstance(AutoGroupType agt) : base(agt)
    {
    }

    public override void OnInstanceCreate(WorldMapInstance instance)
    {
        base.OnInstanceCreate(instance);
        PvPArenaScore score = (PvPArenaScore)instance.GetInstanceHandler().GetInstanceScore();
        score.SetDifficultyId(agt.GetDifficultId());
    }

    public override AGQuestion AddLookingForParty(LookingForParty lookingForParty)
    {
        lock (this)
        {
            if (IsRegistrationDisabled(lookingForParty) || lookingForParty.GetMembers().Count > 1
                || registeredAGPlayers.Count >= GetMaxPlayers())
            {
                return AGQuestion.FAILED;
            }

            foreach (KeyValuePair<int, AGPlayer> kv in lookingForParty.GetMembers())
                registeredAGPlayers[kv.Key] = kv.Value;
            return instance == null && registeredAGPlayers.Count == GetMaxPlayers() ? AGQuestion.READY : AGQuestion.ADDED;
        }
    }

    public override void OnPressEnter(Player player)
    {
        if (agt.IsPvPFFAArena() || agt.IsPvPSoloArena() || agt.IsGloryArena())
        {
            long size = 1;
            int itemId = 186000135;
            if (agt.IsGloryArena())
            {
                size = 3;
                itemId = 186000185;
            }
            if (!RemoveItem(player, itemId, size))
            {
                registeredAGPlayers.TryRemove(player.GetObjectId(), out _);
                PacketSendUtility.SendPacket(player, new SM_AUTO_GROUP(agt.GetTemplate().GetMaskId(), 5));
                if (registeredAGPlayers.Count == 0)
                    AutoGroupService.GetInstance().DestroyIfPossible(this);
                return;
            }
        }
        ((PvPArenaScore)instance.GetInstanceHandler().GetInstanceScore()).PortToPosition(player);
        instance.Register(player.GetObjectId());
    }

    public override void OnLeaveInstance(Player player)
    {
        base.Unregister(player);
    }
}
