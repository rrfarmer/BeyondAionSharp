using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/AbstractCharacterEditPacket (Neon). Base for character create/edit packets; reads name/gender/race/class and the full appearance block. PlayerAppearance/PlayerClass red-tolerated.</summary>
public abstract class AbstractCharacterEditPacket : AionClientPacket
{
    protected string characterName;
    protected Gender gender;
    protected Race race;
    protected PlayerClass? playerClass;
    protected PlayerAppearance playerAppearance;

    public AbstractCharacterEditPacket(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected void ReadBasicInfo(bool ignoreInvalidPlayerClass)
    {
        characterName = Util.ConvertName(ReadS(25)); // client leaks random data here when entering char creation screen for the first time
        gender = ReadD() == 0 ? Gender.MALE : Gender.FEMALE;
        race = ReadD() == 0 ? Race.ELYOS : Race.ASMODIANS;
        playerClass = PlayerClassExtensions.GetPlayerClassById((byte)ReadD(), ignoreInvalidPlayerClass);
    }

    protected void ReadAppearance()
    {
        playerAppearance = new PlayerAppearance();

        playerAppearance.SetVoice(ReadD());
        playerAppearance.SetSkinRGB(ReadD());
        playerAppearance.SetHairRGB(ReadD());
        playerAppearance.SetEyeRGB(ReadD());
        playerAppearance.SetLipRGB(ReadD());
        playerAppearance.SetFace(ReadUC());
        playerAppearance.SetHair(ReadUC());
        playerAppearance.SetDeco(ReadUC());
        playerAppearance.SetTattoo(ReadUC());
        playerAppearance.SetFaceContour(ReadUC());
        playerAppearance.SetExpression(ReadUC());
        ReadC(); // always 4 o0 // 5 in 1.5.x
        playerAppearance.SetJawLine(ReadUC());
        playerAppearance.SetForehead(ReadUC());

        playerAppearance.SetEyeHeight(ReadUC());
        playerAppearance.SetEyeSpace(ReadUC());
        playerAppearance.SetEyeWidth(ReadUC());
        playerAppearance.SetEyeSize(ReadUC());
        playerAppearance.SetEyeShape(ReadUC());
        playerAppearance.SetEyeAngle(ReadUC());

        playerAppearance.SetBrowHeight(ReadUC());
        playerAppearance.SetBrowAngle(ReadUC());
        playerAppearance.SetBrowShape(ReadUC());

        playerAppearance.SetNose(ReadUC());
        playerAppearance.SetNoseBridge(ReadUC());
        playerAppearance.SetNoseWidth(ReadUC());
        playerAppearance.SetNoseTip(ReadUC());

        playerAppearance.SetCheek(ReadUC());
        playerAppearance.SetLipHeight(ReadUC());
        playerAppearance.SetMouthSize(ReadUC());
        playerAppearance.SetLipSize(ReadUC());
        playerAppearance.SetSmile(ReadUC());
        playerAppearance.SetLipShape(ReadUC());
        playerAppearance.SetJawHeigh(ReadUC());
        playerAppearance.SetChinJut(ReadUC());
        playerAppearance.SetEarShape(ReadUC());
        playerAppearance.SetHeadSize(ReadUC());

        playerAppearance.SetNeck(ReadUC());
        playerAppearance.SetNeckLength(ReadUC());

        playerAppearance.SetShoulderSize(ReadUC());

        playerAppearance.SetTorso(ReadUC());
        playerAppearance.SetChest(ReadUC()); // only woman
        playerAppearance.SetWaist(ReadUC());
        playerAppearance.SetHips(ReadUC());

        playerAppearance.SetArmThickness(ReadUC());

        playerAppearance.SetHandSize(ReadUC());
        playerAppearance.SetLegThickness(ReadUC());

        playerAppearance.SetFootSize(ReadUC());
        playerAppearance.SetFacialRate(ReadUC());

        ReadC(); // always 0
        playerAppearance.SetArmLength(ReadUC());
        playerAppearance.SetLegLength(ReadUC()); // wrong??
        playerAppearance.SetShoulders(ReadUC()); // 1.5.x May be ShoulderSize
        playerAppearance.SetFaceShape(ReadUC());
        ReadC();
        ReadC();
        ReadC();
        playerAppearance.SetHeight(ReadF());
    }
}
