import com.aionemu.loginserver.network.ncrypt.EncryptedRSAKeyPair;
import com.aionemu.loginserver.network.ncrypt.BlowfishCipher;
import com.aionemu.loginserver.network.ncrypt.CryptEngine;
import com.aionemu.loginserver.network.aion.AionServerPacket;
import com.aionemu.loginserver.network.aion.AionAuthResponse;
import com.aionemu.loginserver.network.aion.LoginConnection;
import com.aionemu.loginserver.network.aion.SessionKey;
import com.aionemu.loginserver.network.aion.serverpackets.SM_ACCOUNT_BANNED;
import com.aionemu.loginserver.network.aion.serverpackets.SM_ACCOUNT_BANNED_2;
import com.aionemu.loginserver.network.aion.serverpackets.SM_ACCOUNT_KICK;
import com.aionemu.loginserver.network.aion.serverpackets.SM_AUTH_GG;
import com.aionemu.loginserver.network.aion.serverpackets.SM_LOGIN_FAIL;
import com.aionemu.loginserver.network.aion.serverpackets.SM_LOGIN_OK;
import com.aionemu.loginserver.network.aion.serverpackets.SM_PLAY_FAIL;
import com.aionemu.loginserver.network.aion.serverpackets.SM_PLAY_OK;
import com.aionemu.loginserver.network.aion.serverpackets.SM_SERVER_LIST;
import com.aionemu.loginserver.network.aion.serverpackets.SM_UPDATE_SESSION;
import com.aionemu.loginserver.network.gameserver.GsConnection;
import com.aionemu.loginserver.network.gameserver.GsAuthResponse;
import com.aionemu.loginserver.network.gameserver.GsServerPacket;
import com.aionemu.loginserver.network.gameserver.serverpackets.SM_ACCOUNT_AUTH_RESPONSE;
import com.aionemu.loginserver.network.gameserver.serverpackets.SM_ACCOUNT_RECONNECT_KEY;
import com.aionemu.loginserver.network.gameserver.serverpackets.SM_BAN_RESPONSE;
import com.aionemu.loginserver.network.gameserver.serverpackets.SM_GS_AUTH_RESPONSE;
import com.aionemu.loginserver.network.gameserver.serverpackets.SM_GS_CHARACTER_RESPONSE;
import com.aionemu.loginserver.network.gameserver.serverpackets.SM_HDDBAN_LIST;
import com.aionemu.loginserver.network.gameserver.serverpackets.SM_LS_CONTROL_RESPONSE;
import com.aionemu.loginserver.network.gameserver.serverpackets.SM_MACBAN_LIST;
import com.aionemu.loginserver.network.gameserver.serverpackets.SM_PING;
import com.aionemu.loginserver.network.gameserver.serverpackets.SM_PREMIUM_RESPONSE;
import com.aionemu.loginserver.network.gameserver.serverpackets.SM_PTRANSFER_RESPONSE;
import com.aionemu.loginserver.network.gameserver.serverpackets.SM_REQUEST_KICK_ACCOUNT;
import com.aionemu.loginserver.GameServerInfo;
import com.aionemu.loginserver.GameServerTable;
import com.aionemu.loginserver.controller.AccountController;
import com.aionemu.loginserver.controller.BannedHDDController;
import com.aionemu.loginserver.controller.BannedMacManager;
import com.aionemu.loginserver.model.Account;
import com.aionemu.loginserver.model.AccountTime;
import com.aionemu.loginserver.model.base.BannedMacEntry;
import com.aionemu.loginserver.service.ptransfer.PlayerTransferRequest;
import com.aionemu.loginserver.service.ptransfer.PlayerTransferResultStatus;
import com.aionemu.loginserver.service.ptransfer.PlayerTransferStatus;
import com.aionemu.loginserver.service.ptransfer.PlayerTransferTask;
import java.math.BigInteger;
import java.lang.reflect.Method;
import java.security.KeyPair;
import java.security.interfaces.RSAPublicKey;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.sql.Timestamp;
import java.util.Arrays;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.Map;

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

	public static void main(String[] args) throws Exception {
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

		print("SM_AUTH_GG_PAYLOAD", writeAionPayload(new SM_AUTH_GG(0x11223344)));
		SessionKey sessionKey = new SessionKey(1001, 0x11223344, 0x01020304, 0x55667788);
		print("SM_LOGIN_FAIL_PAYLOAD", writeAionPayload(new SM_LOGIN_FAIL(AionAuthResponse.STR_L2AUTH_S_INCORRECT_PWD)));
		print("SM_LOGIN_OK_PAYLOAD", writeAionPayload(new SM_LOGIN_OK(sessionKey)));
		print("SM_PLAY_FAIL_PAYLOAD", writeAionPayload(new SM_PLAY_FAIL(AionAuthResponse.STR_L2AUTH_S_SERVER_DOWN)));
		print("SM_PLAY_OK_PAYLOAD", writeAionPayload(new SM_PLAY_OK(sessionKey, (byte) 7)));
		print("SM_ACCOUNT_KICK_PAYLOAD", writeAionPayload(new SM_ACCOUNT_KICK(AionAuthResponse.STR_L2AUTH_S_BLOCKED_IP)));
		print("SM_ACCOUNT_BANNED_PAYLOAD", writeAionPayload(new SM_ACCOUNT_BANNED()));
		print("SM_ACCOUNT_BANNED_2_PAYLOAD", writeAionPayload(new SM_ACCOUNT_BANNED_2()));
		print("SM_UPDATE_SESSION_PAYLOAD", writeAionPayload(new SM_UPDATE_SESSION(sessionKey)));
		GameServerTable.setGameServers(Arrays.asList(new GameServerInfo((byte) 1, new byte[] { 127, 0, 0, 1 }, 7777, 0, 100, true)));
		Map<Byte, Integer> characterCounts = new HashMap<Byte, Integer>();
		characterCounts.put((byte) 1, 2);
		AccountController.setGSCharacterCountsFor(1001, characterCounts);
		print("SM_SERVER_LIST_PAYLOAD", writeAionPayload(new SM_SERVER_LIST(), new LoginConnection(new Account(1001, 1))));
		print("SM_GS_AUTH_RESPONSE_AUTHED_PAYLOAD", writeGsPayload(new SM_GS_AUTH_RESPONSE(GsAuthResponse.AUTHED)));
		print("SM_GS_AUTH_RESPONSE_NOT_AUTHED_PAYLOAD", writeGsPayload(new SM_GS_AUTH_RESPONSE(GsAuthResponse.NOT_AUTHED)));
		AccountTime accountTime = new AccountTime();
		accountTime.setAccumulatedOnlineTime(1111L);
		accountTime.setAccumulatedRestTime(2222L);
		Account account = new Account(1001, 1, "player", accountTime);
		GameServerInfo accountAuthGameServer = new GameServerInfo((byte) 1, new byte[] { 127, 0, 0, 1 }, 7777, 0, 100, true);
		accountAuthGameServer.addAccount(account);
		GsConnection accountAuthConnection = new GsConnection(accountAuthGameServer);
		print("SM_ACCOUNT_AUTH_RESPONSE_OK_PAYLOAD", writeGsPayload(
			new SM_ACCOUNT_AUTH_RESPONSE(1001, true, "player", 1700000000000L, (byte) 3, (byte) 2, 1500L, "disk-1"),
			accountAuthConnection));
		print("SM_ACCOUNT_AUTH_RESPONSE_FAIL_PAYLOAD", writeGsPayload(
			new SM_ACCOUNT_AUTH_RESPONSE(1001, false, "", 0L, (byte) 0, (byte) 0, 0L, ""),
			accountAuthConnection));
		print("SM_ACCOUNT_RECONNECT_KEY_PAYLOAD", writeGsPayload(new SM_ACCOUNT_RECONNECT_KEY(1001, 0x11223344)));
		print("SM_BAN_RESPONSE_PAYLOAD", writeGsPayload(new SM_BAN_RESPONSE((byte) 3, 99, "127.0.0.1", 15, 12345, true)));
		print("SM_GS_CHARACTER_RESPONSE_PAYLOAD", writeGsPayload(new SM_GS_CHARACTER_RESPONSE(123)));
		Map<String, Timestamp> hddBans = new LinkedHashMap<String, Timestamp>();
		hddBans.put("disk", new Timestamp(1700000000000L));
		BannedHDDController.setMap(hddBans);
		print("SM_HDDBAN_LIST_PAYLOAD", writeGsPayload(new SM_HDDBAN_LIST()));
		print("SM_LS_CONTROL_RESPONSE_PAYLOAD", writeGsPayload(new SM_LS_CONTROL_RESPONSE((byte) 1, (byte) 7, 99, 12345, true)));
		Map<String, BannedMacEntry> macBans = new LinkedHashMap<String, BannedMacEntry>();
		macBans.put("aa-bb", new BannedMacEntry("aa-bb", new Timestamp(1700000000000L), "reason"));
		BannedMacManager.setMap(macBans);
		print("SM_MACBAN_LIST_PAYLOAD", writeGsPayload(new SM_MACBAN_LIST()));
		print("SM_PING_PAYLOAD", writeGsPayload(new SM_PING()));
		print("SM_PREMIUM_RESPONSE_PAYLOAD", writeGsPayload(new SM_PREMIUM_RESPONSE(200, 3, 1500)));
		PlayerTransferTask transferTask = new PlayerTransferTask();
		transferTask.sourceServerId = 1;
		transferTask.targetServerId = 2;
		transferTask.sourceAccountId = 10;
		transferTask.targetAccountId = 20;
		transferTask.playerId = 30;
		transferTask.id = 40;
		print("SM_PTRANSFER_PERFORM_ACTION_PAYLOAD", writeGsPayload(new SM_PTRANSFER_RESPONSE(PlayerTransferResultStatus.PERFORM_ACTION, transferTask)));
		PlayerTransferRequest transferRequest = new PlayerTransferRequest(PlayerTransferStatus.STEP1);
		transferRequest.targetAccountId = 20;
		transferRequest.taskId = 40;
		transferRequest.name = "Character";
		transferRequest.targetAccount = new Account(20, -1, "target", new AccountTime());
		transferRequest.db = new byte[] { 1, 2, 3 };
		print("SM_PTRANSFER_SEND_INFO_PAYLOAD", writeGsPayload(new SM_PTRANSFER_RESPONSE(PlayerTransferResultStatus.SEND_INFO, transferRequest)));
		print("SM_PTRANSFER_OK_PAYLOAD", writeGsPayload(new SM_PTRANSFER_RESPONSE(PlayerTransferResultStatus.OK, 40)));
		print("SM_PTRANSFER_ERROR_PAYLOAD", writeGsPayload(new SM_PTRANSFER_RESPONSE(PlayerTransferResultStatus.ERROR, 40, "nope")));
		print("SM_REQUEST_KICK_ACCOUNT_PAYLOAD", writeGsPayload(new SM_REQUEST_KICK_ACCOUNT(123, true)));
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

	private static void print(String label, byte[] data) {
		print(label, data, data.length);
	}

	private static byte[] writeAionPayload(AionServerPacket packet) throws Exception {
		return writeAionPayload(packet, null);
	}

	private static byte[] writeAionPayload(AionServerPacket packet, LoginConnection connection) throws Exception {
		ByteBuffer buffer = ByteBuffer.allocate(256).order(ByteOrder.LITTLE_ENDIAN);
		packet.setBuf(buffer);
		buffer.put((byte) packet.getOpCode());
		Method writeImpl = packet.getClass().getDeclaredMethod("writeImpl", LoginConnection.class);
		writeImpl.setAccessible(true);
		writeImpl.invoke(packet, connection);
		return toArray(buffer);
	}

	private static byte[] writeGsPayload(GsServerPacket packet) throws Exception {
		return writeGsPayload(packet, null);
	}

	private static byte[] writeGsPayload(GsServerPacket packet, GsConnection connection) throws Exception {
		ByteBuffer buffer = ByteBuffer.allocate(256).order(ByteOrder.LITTLE_ENDIAN);
		packet.setBuf(buffer);
		Method writeImpl = packet.getClass().getDeclaredMethod("writeImpl", GsConnection.class);
		writeImpl.setAccessible(true);
		writeImpl.invoke(packet, connection);
		return toArray(buffer);
	}

	private static byte[] toArray(ByteBuffer buffer) {
		byte[] result = new byte[buffer.position()];
		buffer.flip();
		buffer.get(result);
		return result;
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
