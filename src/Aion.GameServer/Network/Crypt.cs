using System;
using Aion.Commons.Nio;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Network.Aion.ServerPackets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Network;

/// <summary>Crypt will encrypt server packet and decrypt client packet. Java parity: network/Crypt (hack99, kao, -Nemesiss-). int hex literals wrap as int → unchecked((int)0x...); IllegalStateException→InvalidOperationException.</summary>
public class Crypt
{
    private static readonly ILogger log = NullLogger.Instance;

    /// <summary>Second byte of server packet must be equal to this.</summary>
    public const byte staticServerPacketCode = 0x44;

    /// <summary>Crypt is enabled after first server packet was sent.</summary>
    private bool isEnabled;

    private EncryptionKeyPair packetKey = null;

    /// <summary>
    /// Enable crypt key - generate random key used to encrypt second server packet (first is unencrypted) and decrypt client packets.
    /// Called from SM_KEY server packet.
    /// </summary>
    /// <returns>Enciphered key which the client will decipher and use for all future (encrypted) communication.</returns>
    public int EnableKey()
    {
        if (packetKey != null)
            throw new InvalidOperationException("Key is already initialized");

        // rnd key - this will be used to encrypt/decrypt packets
        int key = Rnd.NextInt();

        packetKey = new EncryptionKeyPair(key);

        log.LogDebug("new encrypt key: {Key}", packetKey);

        // enciphered key that will be sent to aion client in SM_KEY packet
        return unchecked((key ^ unchecked((int)0xCD92E4DF)) + 0x3FF2CCCF);
    }

    public bool IsEnabled()
    {
        return isEnabled;
    }

    /// <summary>Decrypt client packet from this ByteBuffer.</summary>
    /// <returns>true if decryption was successful.</returns>
    public bool Decrypt(ByteBuffer buf)
    {
        return packetKey.Decrypt(buf);
    }

    /// <summary>Encrypt server packet from this ByteBuffer.</summary>
    public void Encrypt(ByteBuffer buf)
    {
        if (!isEnabled)
        {
            // first server packet (SM_KEY) is not encrypted because it sends the enciphered crypt key
            isEnabled = true;
            return;
        }

        packetKey.Encrypt(buf);
    }

    /// <returns>Obfuscated server packet opcode.</returns>
    public static int EncodeServerPacketOpcode(int opcode)
    {
        return (opcode + SM_VERSION_CHECK.INTERNAL_VERSION) ^ 0xDF;
    }

    /// <returns>Deobfuscated client packet opcode.</returns>
    public static int DecodeClientPacketOpcode(int opcode)
    {
        return ((opcode ^ 0xEF) - 0xC ^ 0xEF) - SM_VERSION_CHECK.INTERNAL_VERSION;
    }
}
