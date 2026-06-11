using System.Collections.Generic;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_NPC_INFO (-Nemesiss-). Visible npc/monster/summon info. getType(player)->GetType_() collision; CreatureMoveController&lt;?&gt;->&lt;Creature&gt;; gear iterable -> foreach KeyValuePair. Npc/Summon/NpcTemplate/NpcEquippedGear/TownService red-tolerated.</summary>
public class SM_NPC_INFO : AionServerPacket
{
    private Creature npc;
    private int creatorId;
    private string masterName;
    private CreatureType creatureType;

    public SM_NPC_INFO(Npc npc, Player player)
    {
        this.npc = npc;
        creatorId = npc.GetCreatorId();
        masterName = npc.GetMasterName();
        creatureType = npc.GetType_(player);
    }

    public SM_NPC_INFO(Summon summon, Player player)
    {
        this.npc = summon;
        Player owner = summon.GetMaster();
        creatorId = owner.GetObjectId();
        masterName = owner.GetName();
        creatureType = summon.GetType_(player);
    }

    protected override void WriteImpl(AionConnection con)
    {
        NpcTemplate npcTemplate = (NpcTemplate)npc.GetObjectTemplate();
        CreatureMoveController<Creature> mc = npc.GetMoveController();

        WriteF(npc.GetX());
        WriteF(npc.GetY());
        WriteF(npc.GetZ());
        WriteD(npc.GetObjectId());
        WriteD(npcTemplate.GetTemplateId()); // npc id reference for hp gauge + talk properties
        WriteD(npcTemplate.GetTemplateId()); // npc id reference for visual appearance
        WriteC(creatureType.GetId());

        /*
         * 3,19 - wings spread
         * 5,6,11,21 - sitting
         * 7,23,71 - dead, no drop
         * 8,24 - dead, looks like some orb of light (no normal mesh)
         * 32,33 - fight mode
         * 65 - normal
         */
        WriteH(npc.GetState());

        WriteC(npc.GetHeading());
        WriteD(npcTemplate.GetL10nId());
        WriteD(npcTemplate.GetTitleId());// TODO: implement fortress titles

        WriteH(0x00);// unk
        WriteC(0x00);// unk
        WriteD(0x00);// unk

        /*
         * Creator/Master Info (Summon, Kisk, Etc)
         */
        WriteD(creatorId);// creatorId - playerObjectId or House address
        WriteS(masterName);// masterName

        WriteC(npc.GetLifeStats().GetHpPercentage());
        WriteD(npc.GetGameStats().GetMaxHp().GetCurrent());
        WriteC(npc.GetLevel());

        NpcEquippedGear gear = npc.GetOverrideEquipment(); // dynamically overriden Equipment (only for NPCs, not summons)
        if (gear == null)
        {
            WriteD(0x00);
        }
        else
        {
            WriteD(gear.GetItemsMask());
            bool hasWeapon = false;
            // getting it from template (later if we make sure that npcs actually use items, we'll make Item from it)
            foreach (KeyValuePair<ItemSlot, ItemTemplate> item in gear)
            {
                if (!hasWeapon)
                    hasWeapon = item.Value.IsWeapon();
                WriteD(item.Value.GetTemplateId());
                WriteD(0x00);
                WriteD(0x00);
                WriteH(0x00);
                WriteH(0x00); // 4.7
            }
        }
        WriteF(npcTemplate.GetBoundRadius().GetMaxOfFrontAndSide());
        WriteF(npcTemplate.GetHeight());
        WriteF(npc.GetGameStats().GetMovementSpeedFloat());// speed
        WriteH(npcTemplate.GetAttackSpeed());
        WriteH(npcTemplate.GetAttackSpeed());
        WriteC(npc.IsFlag() ? 0x13 : npc.IsNewSpawn() ? 0x01 : 0x00);
        WriteF(mc.GetTargetX2());
        WriteF(mc.GetTargetY2());
        WriteF(mc.GetTargetZ2());
        WriteC(mc.GetMovementMask()); // move type
        WriteH(npc.GetSpawn() == null ? 0 : npc.GetSpawn().GetStaticId());
        WriteC(0);
        WriteC(0); // all unknown
        WriteC(0);
        WriteC(0);
        WriteC(0);
        WriteC(0);
        WriteC(0);
        WriteC(0);
        WriteC(npc.GetVisualState()); // visualState
        WriteH(npc.GetNpcObjectType().GetId());
        WriteC(0x00); // unk
        WriteD(npc.GetTarget() == null ? 0 : npc.GetTarget().GetObjectId());
        WriteD(TownService.GetInstance().GetTownIdByPosition(npc));
        WriteD(0);// unk 4.7.5
    }
}
