import com.aionemu.loginserver.network.ncrypt.BlowfishCipher;
import com.aionemu.loginserver.network.ncrypt.CryptEngine;

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
	}

	private static void print(String label, byte[] data, int length) {
		StringBuilder sb = new StringBuilder(label).append('=');
		for (int i = 0; i < length; i++)
			sb.append(String.format("%02X", data[i] & 0xFF));
		System.out.println(sb);
	}
}
