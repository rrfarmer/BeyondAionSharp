using System;
using System.Text;
using Aion.Commons.Nio;

namespace Aion.GameServer.Network;

/// <summary>
/// Java parity: network/EncryptionKeyPair (cura). Aion packet cipher. Java signed byte[] → C# byte[] (unsigned; all key/data
/// math uses &amp; 0xff so values are bit-identical); System.arraycopy→Array.Copy; currentTimeMillis()→ToUnixTimeMilliseconds;
/// Integer.toHexString→ToString("x"); byte ^= int → byte = (byte)(byte ^ int).
/// </summary>
public class EncryptionKeyPair
{
    /// <summary>keys index to access SERVER encryption key.</summary>
    private const int SERVER = 0;

    /// <summary>keys index to access CLIENT encryption key.</summary>
    private const int CLIENT = 1;

    /// <summary>Static xor key.</summary>
    private static readonly byte[] staticKey = Encoding.ASCII.GetBytes("nKO/WctQ0AVLbpzfBkS6NevDYT8ourG5CRlmdjyJ72aswx4EPq1UgZhFMXH?3iI9");

    /// <summary>Second byte of client packet must be equal to this.</summary>
    private const byte staticClientPacketCode = 0x65;

    /// <summary>Base key used to generate client/server keys.</summary>
    private int baseKey = 0;

    /// <summary>Encryption keys.</summary>
    private byte[][] keys = null;

    /// <summary>Date of last key use.</summary>
    private long lastUpdate;

    /// <summary>Initializes client/server encryption keys based on baseKey (random integer).</summary>
    public EncryptionKeyPair(int baseKey)
    {
        this.baseKey = baseKey;
        this.keys = new byte[2][];
        this.keys[SERVER] = new byte[] { (byte)(baseKey & 0xff), (byte)((baseKey >> 8) & 0xff), (byte)((baseKey >> 16) & 0xff),
            (byte)((baseKey >> 24) & 0xff), 0xa1, 0x6c, 0x54, 0x87 };
        this.keys[CLIENT] = new byte[this.keys[SERVER].Length];
        Array.Copy(this.keys[SERVER], 0, this.keys[CLIENT], 0, this.keys[SERVER].Length);
        this.lastUpdate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <returns>the baseKey used to generate the key pair</returns>
    public int GetBaseKey()
    {
        return baseKey;
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{client:0x");
        for (int i = 0; i < keys[CLIENT].Length; i++)
        {
            sb.Append((keys[CLIENT][i] & 0xff).ToString("x"));
        }
        sb.Append(",server:0x");
        for (int i = 0; i < keys[SERVER].Length; i++)
        {
            sb.Append((keys[SERVER][i] & 0xff).ToString("x"));
        }
        sb.Append(",base:0x");
        sb.Append(baseKey.ToString("x"));
        sb.Append(",update:" + lastUpdate + "}");
        return sb.ToString();
    }

    /// <summary>Check if packet was correctly decoded, also check if packet was correctly coded by aion client.</summary>
    private bool ValidateClientPacket(ByteBuffer buf)
    {
        return buf.Limit() >= 5 && buf.GetShort(0) == ~buf.GetShort(3) && buf.Get(2) == staticClientPacketCode;
    }

    /// <summary>Decrypt client packet from this ByteBuffer; if successful, update client key.</summary>
    public bool Decrypt(ByteBuffer buf)
    {
        byte[] data = buf.Array();
        int size = buf.Remaining();
        byte[] clientPacketKey = keys[CLIENT];

        // index to byte that should be decrypted now
        int arrayIndex = buf.ArrayOffset() + buf.Position();

        // prev encrypted byte
        int prev = data[arrayIndex];

        // decrypt first byte
        data[arrayIndex] = (byte)(data[arrayIndex] ^ (clientPacketKey[0] & 0xff));
        arrayIndex++;

        // decrypt loop
        for (int i = 1; i < size; i++, arrayIndex++)
        {
            int curr = data[arrayIndex] & 0xff;
            data[arrayIndex] = (byte)(data[arrayIndex] ^ (staticKey[i & 63] & 0xff) ^ (clientPacketKey[i & 7] & 0xff) ^ prev);
            prev = curr;
        }

        // oldKey value as long
        long oldKey = (((long)clientPacketKey[0] & 0xff) << 0) | (((long)clientPacketKey[1] & 0xff) << 8) | (((long)clientPacketKey[2] & 0xff) << 16)
            | (((long)clientPacketKey[3] & 0xff) << 24) | (((long)clientPacketKey[4] & 0xff) << 32) | (((long)clientPacketKey[5] & 0xff) << 40)
            | (((long)clientPacketKey[6] & 0xff) << 48) | (((long)clientPacketKey[7] & 0xff) << 56);

        // change key
        oldKey += size;

        if (ValidateClientPacket(buf))
        {
            // set key new value
            clientPacketKey[0] = (byte)(oldKey >> 0 & 0xff);
            clientPacketKey[1] = (byte)(oldKey >> 8 & 0xff);
            clientPacketKey[2] = (byte)(oldKey >> 16 & 0xff);
            clientPacketKey[3] = (byte)(oldKey >> 24 & 0xff);
            clientPacketKey[4] = (byte)(oldKey >> 32 & 0xff);
            clientPacketKey[5] = (byte)(oldKey >> 40 & 0xff);
            clientPacketKey[6] = (byte)(oldKey >> 48 & 0xff);
            clientPacketKey[7] = (byte)(oldKey >> 56 & 0xff);
            return true;
        }
        return false;
    }

    /// <summary>Encrypt server packet from this ByteBuffer.</summary>
    public void Encrypt(ByteBuffer buf)
    {
        byte[] data = buf.Array();
        int size = buf.Remaining();
        byte[] serverPacketKey = keys[SERVER];

        // index to byte that should be encrypted now
        int arrayIndex = buf.ArrayOffset() + buf.Position();

        // encrypt first byte
        data[arrayIndex] = (byte)(data[arrayIndex] ^ (serverPacketKey[0] & 0xff));

        // prev encrypted byte
        int prev = data[arrayIndex];
        arrayIndex++;

        // encrypt loop
        for (int i = 1; i < size; i++, arrayIndex++)
        {
            data[arrayIndex] = (byte)(data[arrayIndex] ^ (staticKey[i & 63] & 0xff) ^ (serverPacketKey[i & 7] & 0xff) ^ prev);
            prev = data[arrayIndex];
        }

        // oldKey value as long
        long oldKey = (((long)serverPacketKey[0] & 0xff) << 0) | (((long)serverPacketKey[1] & 0xff) << 8) | (((long)serverPacketKey[2] & 0xff) << 16)
            | (((long)serverPacketKey[3] & 0xff) << 24) | (((long)serverPacketKey[4] & 0xff) << 32) | (((long)serverPacketKey[5] & 0xff) << 40)
            | (((long)serverPacketKey[6] & 0xff) << 48) | (((long)serverPacketKey[7] & 0xff) << 56);

        // change key
        oldKey += size;

        // set key new value
        serverPacketKey[0] = (byte)(oldKey >> 0 & 0xff);
        serverPacketKey[1] = (byte)(oldKey >> 8 & 0xff);
        serverPacketKey[2] = (byte)(oldKey >> 16 & 0xff);
        serverPacketKey[3] = (byte)(oldKey >> 24 & 0xff);
        serverPacketKey[4] = (byte)(oldKey >> 32 & 0xff);
        serverPacketKey[5] = (byte)(oldKey >> 40 & 0xff);
        serverPacketKey[6] = (byte)(oldKey >> 48 & 0xff);
        serverPacketKey[7] = (byte)(oldKey >> 56 & 0xff);
    }
}
