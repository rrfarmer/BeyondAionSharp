using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Aion.Commons.Network;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.FindGroup;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

/// <summary>
/// Faithful golden coverage for network/aion/serverpackets/SM_FIND_GROUP, ported 1:1 from
/// game-server/test/.../SM_FIND_GROUP_GoldenTest.java. Drives the live FindGroup model graph
/// (GroupRecruitment/GroupApplication/ServerWideGroup/Player) exactly as FindGroupService does,
/// instead of the retired flat-snapshot reworked packet. Java pokes private fields via Unsafe;
/// C# uses reflection field-set (lastUpdate) and SetPosition for the worldId-bearing case.
/// </summary>
public sealed class SmFindGroupTests
{
    [Fact]
    public void WriteImpl_RemoveRecruitmentMatchesJavaGolden()
    {
        var payload = Write(new SM_FIND_GROUP(700101, (byte)5, (byte)6, (byte)7, (byte)8));

        Assert.Equal("01C5AE0A0005060708", ToHex(payload));
    }

    [Fact]
    public void WriteImpl_RemoveApplicationMatchesJavaGolden()
    {
        var payload = Write(new SM_FIND_GROUP(700105));

        Assert.Equal("05C9AE0A00", ToHex(payload));
    }

    [Fact]
    public void WriteImpl_EnableRegisterForInstancesMatchesJavaGolden()
    {
        var payload = Write(new SM_FIND_GROUP(new List<int> { 300110000, 300150000 }));

        Assert.Equal("1A0200B050E311F0ECE311", ToHex(payload));
    }

    [Fact]
    public void WriteImpl_ShowApplicationsWritesTimestampedApplicationSnapshot()
    {
        WithNameTags([], () =>
        {
            var application = new GroupApplication(
                SimplePlayer(0x01020304, "Applicant", PlayerClass.RANGER, 65),
                "Apply", 1, PlayerClass.RANGER.GetClassId(), 65);
            SetField(application, "lastUpdate", 0x01020305);
            var packet = new SM_FIND_GROUP(4, new List<GroupApplication> { application });

            int before = NowSeconds();
            var payload = Write(packet);
            int after = NowSeconds();

            var reader = new HexReader(payload);
            Assert.Equal(4, reader.C());
            Assert.Equal(1, reader.H());
            Assert.Equal(1, reader.H());
            int header = reader.D();
            Assert.InRange(header, before, after);
            Assert.Equal(0x01020304, reader.D());
            Assert.Equal(1, reader.C());
            Assert.Equal("Apply", reader.S());
            Assert.Equal("Applicant", reader.S());
            Assert.Equal((int)PlayerClass.RANGER.GetClassId(), reader.C());
            Assert.Equal(65, reader.C());
            Assert.Equal(0x01020305, reader.D());
            Assert.True(reader.AtEnd);
        });
    }

    [Fact]
    public void WriteImpl_ShowRecruitmentsWritesTimestampedSoloRecruitmentSnapshot()
    {
        var originalGameServerId = NetworkConfig.GAMESERVER_ID;
        WithNameTags([], () =>
        {
            try
            {
                NetworkConfig.GAMESERVER_ID = 1;
                var recruitment = new GroupRecruitment(
                    SimplePlayer(0x01020304, "Recruiter", PlayerClass.GLADIATOR, 65), "LFG", 2);
                SetField(recruitment, "lastUpdate", 0x01020305);
                var packet = new SM_FIND_GROUP(0, new List<GroupRecruitment> { recruitment });

                int before = NowSeconds();
                var payload = Write(packet);
                int after = NowSeconds();

                var reader = new HexReader(payload);
                Assert.Equal(0, reader.C());
                Assert.Equal(1, reader.H());
                Assert.Equal(1, reader.H());
                int header = reader.D();
                Assert.InRange(header, before, after);
                Assert.Equal(0x01020304, reader.D());
                Assert.Equal(1, reader.C());
                Assert.Equal(0, reader.C());
                Assert.Equal(0, reader.C());
                Assert.Equal(16, reader.C());
                Assert.Equal(2, reader.C());
                Assert.Equal("LFG", reader.S());
                Assert.Equal("Recruiter", reader.S());
                Assert.Equal(1, reader.C());
                Assert.Equal(65, reader.C());
                Assert.Equal(65, reader.C());
                Assert.Equal(0x01020305, reader.D());
                Assert.True(reader.AtEnd);
            }
            finally
            {
                NetworkConfig.GAMESERVER_ID = originalGameServerId;
            }
        });
    }

    [Fact]
    public void WriteImpl_ShowInstanceGroupsWritesTimestampedGroupSnapshot()
    {
        WithNameTags([], () =>
        {
            var group = SimpleInstanceGroup();
            SetField(group, "lastUpdate", 0x01020305);
            var packet = new SM_FIND_GROUP(10, new List<ServerWideGroup> { group });

            int before = NowSeconds();
            var payload = Write(packet);
            int after = NowSeconds();

            var reader = new HexReader(payload);
            Assert.Equal(10, reader.C());
            Assert.Equal(1, reader.H());
            Assert.Equal(1, reader.H());
            int header = reader.D();
            Assert.InRange(header, before, after);
            Assert.Equal(0x01020304, reader.D());
            Assert.Equal(0x11223344, reader.D());
            Assert.Equal(1, reader.D());
            Assert.Equal(1, reader.C());
            Assert.Equal(3, reader.C());
            Assert.Equal(0, reader.H());
            Assert.Equal(0x01020304, reader.D());
            Assert.Equal(1, reader.D());
            Assert.Equal(0, reader.D());
            Assert.Equal(65, reader.C());
            Assert.Equal(65, reader.C());
            Assert.Equal(0, reader.H());
            Assert.Equal(0x01020305, reader.D());
            Assert.Equal(0, reader.D());
            Assert.Equal("Recruiter", reader.S());
            Assert.Equal("Entry", reader.S());
            Assert.True(reader.AtEnd);
        });
    }

    [Fact]
    public void WriteImpl_InstanceApplicationWhisperMatchesJavaGolden()
    {
        WithNameTags([], () =>
        {
            var packet = new SM_FIND_GROUP(SimplePlayer(0x01020304, "Applicant", PlayerClass.RANGER, 65));
            var payload = Write(packet);

            Assert.Equal(
                "0B04030201000000000000000000000005410000004100700070006C006900630061006E0074000000",
                ToHex(payload));
        });
    }

    [Fact]
    public void WriteImpl_ShowEnterButtonMatchesJavaGolden()
    {
        var packet = new SM_FIND_GROUP(18, new List<FindGroupEntry> { SimpleInstanceGroup() });
        var payload = Write(packet);

        Assert.Equal("120403020144332211", ToHex(payload));
    }

    [Fact]
    public void WriteImpl_ShowPrepareWindowMatchesJavaGolden()
    {
        var packet = new SM_FIND_GROUP(22, new List<FindGroupEntry> { SimpleInstanceGroup() });
        var payload = Write(packet);

        Assert.Equal("160403020144332211", ToHex(payload));
    }

    [Fact]
    public void WriteImpl_RegisterInstanceGroupMatchesJavaGolden()
    {
        WithNameTags([], () =>
        {
            var group = SimpleInstanceGroup();
            SetField(group, "lastUpdate", 0x01020305);
            var packet = new SM_FIND_GROUP(14, new List<ServerWideGroup> { group });
            var payload = Write(packet);

            Assert.Equal(
                "0E0104030201443322110100000001030000040302010100010000000000414100000503020100000000520065006300720075006900740065007200000045006E007400720079000000",
                ToHex(payload));
        });
    }

    [Fact]
    public void WriteImpl_DestroyPrepareWindowMatchesJavaGolden()
    {
        var packet = new SM_FIND_GROUP(23, new List<FindGroupEntry> { SimpleInstanceGroup() });
        var payload = Write(packet);

        Assert.Equal("17040302014433221100", ToHex(payload));
    }

    [Fact]
    public void WriteImpl_UpdatePrepareWindowMatchesJavaGolden()
    {
        WithNameTags([], () =>
        {
            var group = SimpleInstanceGroup();
            var packet = new SM_FIND_GROUP(24, new List<FindGroupEntry> { group });
            var payload = Write(packet);

            Assert.Equal(
                "180403020144332211010000000000000000040302014100000001000000000001005200650063007200750069007400650072000000",
                ToHex(payload));
        });
    }

    [Fact]
    public void WriteImpl_ShowInstanceGroupMemberInfoWritesTimestampedMemberSnapshot()
    {
        WithNameTags([], () =>
        {
            var group = SimpleInstanceGroup();
            var packet = new SM_FIND_GROUP(16, new List<FindGroupEntry> { group });

            int before = NowSeconds();
            var payload = Write(packet);
            int after = NowSeconds();

            var reader = new HexReader(payload);
            Assert.Equal(16, reader.C());
            Assert.Equal(1, reader.H());
            Assert.Equal(1, reader.H());
            int header = reader.D();
            Assert.InRange(header, before, after);
            Assert.Equal(0, reader.D());
            Assert.Equal(300110000, reader.D());
            Assert.Equal(0x01020304, reader.D());
            Assert.Equal(65, reader.D());
            Assert.Equal((int)PlayerClass.GLADIATOR.GetClassId(), reader.D());
            Assert.Equal(1, reader.H());
            Assert.Equal(0, reader.C());
            Assert.Equal(0, reader.C());
            Assert.Equal("Recruiter", reader.S());
            Assert.True(reader.AtEnd);
        });
    }

    // ---- fixtures (mirror SM_FIND_GROUP_GoldenTest.java) ----

    private static ServerWideGroup SimpleInstanceGroup()
    {
        var recruiter = SimplePlayer(0x01020304, "Recruiter", PlayerClass.GLADIATOR, 65);
        return new ServerWideGroup(recruiter, 0x11223344, 3, "Entry");
    }

    private static Player SimplePlayer(int objectId, string name, PlayerClass playerClass, int level)
    {
        // Mirror SM_FIND_GROUP_GoldenTest.simplePlayer: allocate Player without running its ctor
        // (the real ctor builds AbsoluteStatOwner which touches the ABSOLUTE_STATS_DATA DataManager
        // singleton, not wired in a unit test — Java sidesteps it the same way via Unsafe), then poke
        // only the fields the packet writer reads: objectId, playerAccountData, playerAccount, position.
        var common = new PlayerCommonData(objectId);
        common.SetName(name);
        common.SetPlayerClass(playerClass);
        SetField(common, "level", level); // SetLevel touches PLAYER_EXPERIENCE_TABLE; poke directly per Java
        var accountData = new PlayerAccountData(common, new PlayerAppearance());

        var player = (Player)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Player));
        SetField(player, "_objectId", objectId);
        SetField(player, "playerAccountData", accountData);
        SetField(player, "playerAccount", new Account(1));
        SetField(player, "Position", new WorldPosition(300110000));
        return player;
    }

    private static void WithNameTags(string[] tags, Action body)
    {
        var original = AdminConfig.NAME_TAGS;
        try
        {
            AdminConfig.NAME_TAGS = tags;
            body();
        }
        finally
        {
            AdminConfig.NAME_TAGS = original;
        }
    }

    private static int NowSeconds() => (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000);

    private static void SetField(object target, string name, object value)
    {
        var type = target.GetType();
        while (type != null)
        {
            var field = type.GetField(name,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    private static byte[] Write(SM_FIND_GROUP packet)
    {
        // Faithful mirror of SM_FIND_GROUP_GoldenTest.write(): setBuf + writeImpl(null), read raw payload.
        var buffer = Aion.Commons.Nio.ByteBuffer.Allocate(8192).Order(Aion.Commons.Nio.ByteOrder.LITTLE_ENDIAN);
        packet.SetBuf(buffer);
        var writeImpl = typeof(AionServerPacket).GetMethod("WriteImpl",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            new[] { typeof(AionConnection) })!;
        writeImpl.Invoke(packet, new object?[] { null });
        var length = buffer.Position();
        var payload = new byte[length];
        buffer.Flip();
        buffer.Get(payload);
        return payload;
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private sealed class HexReader(byte[] payload)
    {
        private int _pos;

        public bool AtEnd => _pos >= payload.Length;

        public int C() => payload[_pos++];

        public int H() => payload[_pos++] | (payload[_pos++] << 8);

        public int D() => payload[_pos++] | (payload[_pos++] << 8) | (payload[_pos++] << 16) | (payload[_pos++] << 24);

        public string S()
        {
            var sb = new StringBuilder();
            while (true)
            {
                int c = payload[_pos++] | (payload[_pos++] << 8);
                if (c == 0)
                    return sb.ToString();
                sb.Append((char)c);
            }
        }
    }
}
