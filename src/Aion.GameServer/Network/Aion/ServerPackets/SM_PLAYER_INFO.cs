using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Templates.Cp;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.ConquerorAndProtectorSystem;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PLAYER_INFO (-Nemesiss-, Avol, srx47, cura, -Enomine-, -Artur-, Neon, 230L) extends AbstractPlayerInfoPacket. Sends a visible player's full info: position/ids/transform, race/class/gender, name/title/legion-emblem, hp/dp, equipped appearance, full PlayerAppearance, speeds, store message, movement vector, target/team/house/membership, conqueror/protector rank. getType()->GetType_(); getName(true)->GetName(true); movementMask&=~ABSOLUTE; Vector3f normalizeLocal/multLocal. AbstractPlayerInfoPacket/Vector3f/PlayerAppearance/CPInfo red-tolerated.</summary>
public class SM_PLAYER_INFO : AbstractPlayerInfoPacket
{
    private readonly Player player;
    private bool enemy;

    public SM_PLAYER_INFO(Player player)
        : this(player, false)
    {
    }

    public SM_PLAYER_INFO(Player player, bool enemy)
    {
        this.player = player;
        this.enemy = enemy;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player activePlayer = con.GetActivePlayer();
        if (activePlayer == null)
            return;

        PlayerCommonData pcd = player.GetCommonData();
        PlayerAppearance playerAppearance = player.GetPlayerAppearance();
        int raceId = activePlayer.IsEnemy(player) ? activePlayer.GetOppositeRace().GetRaceId() : player.GetRace().GetRaceId();
        if (player.IsInCustomState(CustomPlayerState.NEUTRAL_TO_ALL_PLAYERS) || activePlayer.IsInCustomState(CustomPlayerState.NEUTRAL_TO_ALL_PLAYERS))
            raceId = activePlayer.GetRace().GetRaceId();

        WriteF(player.GetX());// x
        WriteF(player.GetY());// y
        WriteF(player.GetZ());// z
        WriteD(player.GetObjectId());
        WriteD(pcd.GetTemplateId()); // 0xA3 female asmo, 0xA2 male asmo, 0xA1 female elyos, 0xA0 male elyos
        WriteD(player.GetRobotId());// RobotId
        WriteD(player.GetTransformModel().GetModelId()); // transformed model id or player model id
        WriteC(0x00); // new 2.0 Packet --- probably pet info?
        WriteD(player.GetTransformModel().GetType_().GetId());
        WriteC(enemy ? 0x00 : 0x26);

        WriteC(raceId); // race
        WriteC(pcd.GetPlayerClass().GetClassId());
        WriteC(pcd.GetGender().GetGenderId()); // sex
        WriteH(player.GetState());

        WriteD(0);
        bool someState = false;
        WriteD(someState ? 1 : 0);
        if (someState)
            WriteB(new byte[13]); // TODO find out what this data controls

        WriteC(player.GetHeading());

        WriteS(player.GetName(true));

        WriteH(pcd.GetTitleId());
        WriteH(player.GetCommonData().IsHaveMentorFlag() ? 1 : 0);

        WriteH(player.GetCastingSkillId());

        if (player.IsLegionMember())
        {
            WriteD(player.GetLegion().GetLegionId());
            WriteC(player.GetLegion().GetLegionEmblem().GetEmblemId());
            WriteC(player.GetLegion().GetLegionEmblem().GetEmblemType().GetValue());
            WriteC(player.GetLegion().GetLegionEmblem().GetColor_a());
            WriteC(player.GetLegion().GetLegionEmblem().GetColor_r());
            WriteC(player.GetLegion().GetLegionEmblem().GetColor_g());
            WriteC(player.GetLegion().GetLegionEmblem().GetColor_b());
            WriteS(player.GetLegion().GetName());
        }
        else
        {
            WriteB(new byte[12]);
        }
        WriteC(player.GetLifeStats().GetHpPercentage());
        WriteH(pcd.GetDp());// current dp
        WriteC(0x00);// unk (0x00)

        WriteEquippedItems(player.GetEquipment().GetEquippedForAppearance());

        WriteD(playerAppearance.GetSkinRGB());
        WriteD(playerAppearance.GetHairRGB());
        WriteD(playerAppearance.GetEyeRGB());
        WriteD(playerAppearance.GetLipRGB());
        WriteC(playerAppearance.GetFace());
        WriteC(playerAppearance.GetHair());
        WriteC(playerAppearance.GetDeco());
        WriteC(playerAppearance.GetTattoo());
        WriteC(playerAppearance.GetFaceContour());
        WriteC(playerAppearance.GetExpression());

        WriteC(5); // unk 0x05 0x06

        WriteC(playerAppearance.GetJawLine());
        WriteC(playerAppearance.GetForehead());

        WriteC(playerAppearance.GetEyeHeight());
        WriteC(playerAppearance.GetEyeSpace());
        WriteC(playerAppearance.GetEyeWidth());
        WriteC(playerAppearance.GetEyeSize());
        WriteC(playerAppearance.GetEyeShape());
        WriteC(playerAppearance.GetEyeAngle());

        WriteC(playerAppearance.GetBrowHeight());
        WriteC(playerAppearance.GetBrowAngle());
        WriteC(playerAppearance.GetBrowShape());

        WriteC(playerAppearance.GetNose());
        WriteC(playerAppearance.GetNoseBridge());
        WriteC(playerAppearance.GetNoseWidth());
        WriteC(playerAppearance.GetNoseTip());

        WriteC(playerAppearance.GetCheek());
        WriteC(playerAppearance.GetLipHeight());
        WriteC(playerAppearance.GetMouthSize());
        WriteC(playerAppearance.GetLipSize());
        WriteC(playerAppearance.GetSmile());
        WriteC(playerAppearance.GetLipShape());
        WriteC(playerAppearance.GetJawHeigh());
        WriteC(playerAppearance.GetChinJut());
        WriteC(playerAppearance.GetEarShape());
        WriteC(playerAppearance.GetHeadSize());

        WriteC(playerAppearance.GetNeck());
        WriteC(playerAppearance.GetNeckLength());
        WriteC(playerAppearance.GetShoulderSize());

        WriteC(playerAppearance.GetTorso());
        WriteC(playerAppearance.GetChest()); // only woman
        WriteC(playerAppearance.GetWaist());

        WriteC(playerAppearance.GetHips());
        WriteC(playerAppearance.GetArmThickness());
        WriteC(playerAppearance.GetHandSize());
        WriteC(playerAppearance.GetLegThickness());

        WriteC(playerAppearance.GetFootSize());
        WriteC(playerAppearance.GetFacialRate());

        WriteC(0x00); // always 0
        WriteC(playerAppearance.GetArmLength());
        WriteC(playerAppearance.GetLegLength());
        WriteC(playerAppearance.GetShoulders());
        WriteC(playerAppearance.GetFaceShape());
        WriteC(0x00); // always 0

        WriteC(playerAppearance.GetVoice());

        WriteF(playerAppearance.GetHeight());
        WriteF(0.25f); // scale
        WriteF(2.0f); // gravity or slide surface o_O
        float movementSpeed = player.GetGameStats().GetMovementSpeedFloat();
        WriteF(movementSpeed);

        Stat2 attackSpeed = player.GetGameStats().GetAttackSpeed();
        WriteH(attackSpeed.GetBase());
        WriteH(attackSpeed.GetCurrent());
        WriteC(player.GetPortAnimationId()); // not visible to other players

        WriteS(player.HasStore() ? player.GetStore().GetStoreMessage() : ""); // private store message

        PlayerMoveController pmc = player.GetMoveController();
        byte movementMask = pmc.GetMovementMask();
        if ((pmc.movementMask & MovementMask.ABSOLUTE) == MovementMask.ABSOLUTE)
        {
            // calculate the vector, as click to move and target coords are not supported here
            Vector3f vector = new Vector3f(pmc.GetTargetX2() - player.GetX(), pmc.GetTargetY2() - player.GetY(),
                pmc.GetTargetZ2() - player.GetZ()).NormalizeLocal().MultLocal(movementSpeed);
            WriteF(vector.GetX());
            WriteF(vector.GetY());
            WriteF(vector.GetZ());
            movementMask &= unchecked((byte)~MovementMask.ABSOLUTE);
        }
        else
        {
            WriteF(pmc.vectorX);
            WriteF(pmc.vectorY);
            WriteF(pmc.vectorZ);
        }
        WriteF(player.GetX());
        WriteF(player.GetY());
        WriteF(player.GetZ());
        WriteC(movementMask);

        if (player.IsUsingFlightTransporterOrWindstream())
        {
            WriteD(player.GetFlightPath().GetId());
            WriteD(player.GetFlightPath().GetDistance());
        }
        WriteC(player.GetVisualState()); // visualState
        WriteS(player.GetCommonData().GetNote()); // note shown when targeting player

        WriteH(player.GetLevel()); // [level]
        WriteH(player.GetPlayerSettings().GetDisplay()); // unk - 0x04
        WriteH(player.GetPlayerSettings().GetDeny()); // unk - 0x00
        WriteH(player.GetAbyssRank().GetRank().GetId()); // abyss rank
        WriteH(0x00); // unk - 0x01
        WriteD(player.GetTarget() == null ? 0 : player.GetTarget().GetObjectId());
        WriteC(0); // suspect id
        WriteD(player.GetCurrentTeamId());
        WriteC(player.IsMentor() ? 1 : 0);
        WriteD(player.GetActiveHouse() == null ? 0 : player.GetActiveHouse().GetAddress().GetId()); // 3.0

        if (player.GetAccount().GetMembership() > 0)
            WriteD(0x03 + player.GetAccount().GetMembership());// 1 normal, 2 ascension boost, 3 returning, 4 vip 1
        else
            WriteD(0x01);
        WriteD(0x01); // unk 4.7
        WriteC(3); // can be 3 or 5 on elyos side

        CPInfo cpInfo = ConquerorAndProtectorService.GetInstance().GetCPInfoForCurrentMap(player);
        WriteC(cpInfo != null && cpInfo.GetType_() == CPType.CONQUEROR ? cpInfo.GetRank() : 0); // Conqueror rank
        WriteC(cpInfo != null && cpInfo.GetType_() == CPType.PROTECTOR ? cpInfo.GetRank() : 0); // Protector rank

        WriteC(0); // Officer rank icon
    }
}
