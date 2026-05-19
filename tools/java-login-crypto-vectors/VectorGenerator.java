import com.aionemu.loginserver.network.ncrypt.EncryptedRSAKeyPair;
import com.aionemu.loginserver.network.ncrypt.BlowfishCipher;
import com.aionemu.loginserver.network.ncrypt.CryptEngine;
import java.math.BigInteger;
import java.security.KeyPair;
import java.security.interfaces.RSAPublicKey;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;

public final class VectorGenerator {
	private static final byte[] STATIC_KEY = new byte[] {
		(byte) 0x6B, (byte) 0x60, (byte) 0xCB, (byte) 0x5B,
		(byte) 0x82, (byte) 0xCE, (byte) 0x90, (byte) 0xB1,
		(byte) 0xCC, (byte) 0x2B, (byte) 0x6C, (byte) 0x55,
		(byte) 0x6C, (byte) 0x6C, (byte) 0x6C, (byte) 0x6C
	};

	private static final byte[] SESSION_KEY = new byte[] {
		1, 3, 5, 7, 9, 11, 13, 15,
		2, 4, 6, 8, 10, 12, 14, 16
	};

	public static void main(String[] args) {
		byte[] blowfishBlock = new byte[16];
		for (int i = 0; i < blowfishBlock.length; i++)
			blowfishBlock[i] = (byte) i;
		BlowfishCipher cipher = new BlowfishCipher(STATIC_KEY);
		cipher.cipher(blowfishBlock);
		print("BLOWFISH_STATIC_0_15", blowfishBlock, blowfishBlock.length);

		byte[] modulus = new byte[128];
		for (int i = 0; i < modulus.length; i++)
			modulus[i] = (byte) (0x80 + i);
		EncryptedRSAKeyPair rsaPair = new EncryptedRSAKeyPair(new KeyPair(new FixedRsaPublicKey(new BigInteger(1, modulus)), null));
		print("RSA_SCRAMBLED_80_FF", rsaPair.getEncryptedModulus(), rsaPair.getEncryptedModulus().length);

		CryptEngine firstEngine = new CryptEngine();
		firstEngine.updateKey(SESSION_KEY);
		byte[] firstPacket = new byte[64];
		firstPacket[0] = 0x00;
		firstPacket[1] = 0x11;
		firstPacket[2] = 0x22;
		int firstLength = firstEngine.encrypt(firstPacket, 0, 3);
		System.out.println("FIRST_LEN=" + firstLength);
		print("FIRST_ENCRYPTED", firstPacket, firstLength);

		byte[] laterPacket = new byte[64];
		byte[] laterPlain = new byte[] {
			0x03, (byte) 0xE9, 0x03, 0x00, 0x00,
			0x44, 0x33, 0x22, 0x11
		};
		System.arraycopy(laterPlain, 0, laterPacket, 0, laterPlain.length);
		int laterLength = firstEngine.encrypt(laterPacket, 0, laterPlain.length);
		System.out.println("LATER_LEN=" + laterLength);
		print("LATER_ENCRYPTED", laterPacket, laterLength);

		CryptEngine initEngine = new CryptEngine();
		initEngine.updateKey(SESSION_KEY);
		byte[] initPayload = createSmInitPayload();
		byte[] initEncrypted = new byte[initPayload.length + 16];
		System.arraycopy(initPayload, 0, initEncrypted, 0, initPayload.length);
		int initLength = initEngine.encrypt(initEncrypted, 0, initPayload.length);
		byte[] initFrame = new byte[initLength + 2];
		initFrame[0] = (byte) (initFrame.length & 0xFF);
		initFrame[1] = (byte) ((initFrame.length >> 8) & 0xFF);
		System.arraycopy(initEncrypted, 0, initFrame, 2, initLength);
		System.out.println("SM_INIT_LEN=" + initFrame.length);
		print("SM_INIT_FRAME", initFrame, initFrame.length);
	}

	private static byte[] createSmInitPayload() {
		ByteBuffer buffer = ByteBuffer.allocate(256).order(ByteOrder.LITTLE_ENDIAN);
		buffer.put((byte) 0x00);
		buffer.putInt(0x11223344);
		buffer.putInt(0x0000C621);
		for (int i = 0; i < 128; i++)
			buffer.put((byte) i);
		buffer.put(new byte[16]);
		buffer.put(SESSION_KEY);
		buffer.put(new byte[7]);
		buffer.put((byte) 0);
		buffer.putInt(0);
		buffer.putShort((short) 0);
		buffer.put((byte) 0);
		buffer.putInt(0x3FCE09ED);
		buffer.putInt(0);
		byte[] result = new byte[buffer.position()];
		buffer.flip();
		buffer.get(result);
		return result;
	}

	private static void print(String label, byte[] data, int length) {
		StringBuilder sb = new StringBuilder(label).append('=');
		for (int i = 0; i < length; i++)
			sb.append(String.format("%02X", data[i] & 0xFF));
		System.out.println(sb);
	}

	private static final class FixedRsaPublicKey implements RSAPublicKey {
		private final BigInteger modulus;

		private FixedRsaPublicKey(BigInteger modulus) {
			this.modulus = modulus;
		}

		public BigInteger getModulus() {
			return modulus;
		}

		public BigInteger getPublicExponent() {
			return BigInteger.valueOf(65537);
		}

		public String getAlgorithm() {
			return "RSA";
		}

		public String getFormat() {
			return "X.509";
		}

		public byte[] getEncoded() {
			return new byte[0];
		}
	}
}
