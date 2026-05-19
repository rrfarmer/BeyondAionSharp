# Java Login Crypto Vectors

This helper generates login crypto reference bytes from the original Java
`BlowfishCipher` and `CryptEngine` sources. It exists so the C# port can keep
byte-for-byte tests without requiring a JDK on the host machine.

Run from the repository root:

```powershell
$repo = (Get-Location).Path
docker run --rm -v "${repo}:/work" -w /work eclipse-temurin:8-jdk bash -lc "mkdir -p /tmp/aion-vectors/classes && javac -d /tmp/aion-vectors/classes commons/src/com/aionemu/commons/network/packet/BasePacket.java commons/src/com/aionemu/commons/network/packet/BaseServerPacket.java login-server/src/com/aionemu/loginserver/network/ncrypt/BlowfishCipher.java login-server/src/com/aionemu/loginserver/network/ncrypt/CryptEngine.java login-server/src/com/aionemu/loginserver/network/ncrypt/EncryptedRSAKeyPair.java dotnetConversion/tools/java-login-crypto-vectors/com/aionemu/commons/utils/Rnd.java dotnetConversion/tools/java-login-crypto-vectors/com/aionemu/loginserver/model/Account.java dotnetConversion/tools/java-login-crypto-vectors/com/aionemu/loginserver/GameServerInfo.java dotnetConversion/tools/java-login-crypto-vectors/com/aionemu/loginserver/GameServerTable.java dotnetConversion/tools/java-login-crypto-vectors/com/aionemu/loginserver/controller/AccountController.java dotnetConversion/tools/java-login-crypto-vectors/com/aionemu/loginserver/network/aion/LoginConnection.java dotnetConversion/tools/java-login-crypto-vectors/com/aionemu/loginserver/network/gameserver/GsConnection.java login-server/src/com/aionemu/loginserver/network/aion/AionServerPacket.java login-server/src/com/aionemu/loginserver/network/aion/SessionKey.java login-server/src/com/aionemu/loginserver/network/aion/serverpackets/SM_AUTH_GG.java login-server/src/com/aionemu/loginserver/network/aion/serverpackets/SM_LOGIN_OK.java login-server/src/com/aionemu/loginserver/network/aion/serverpackets/SM_PLAY_OK.java login-server/src/com/aionemu/loginserver/network/aion/serverpackets/SM_SERVER_LIST.java login-server/src/com/aionemu/loginserver/network/gameserver/GsServerPacket.java login-server/src/com/aionemu/loginserver/network/gameserver/serverpackets/SM_GS_CHARACTER_RESPONSE.java login-server/src/com/aionemu/loginserver/network/gameserver/serverpackets/SM_REQUEST_KICK_ACCOUNT.java dotnetConversion/tools/java-login-crypto-vectors/VectorGenerator.java && java -cp /tmp/aion-vectors/classes VectorGenerator"
```

Current output:

```text
BLOWFISH_STATIC_0_15=458EF8CB40966A791B9161DBC9042822
RSA_SCRAMBLED_80_FF=0D0F0D134040404040404040404D4F4D534040404040404040404040404040404040404040404040404040404040404040404040404040404040404040404040CDCECFD08485868788898A8B8CCDCECFD09192939495969798999A9B9C9D9E9FA0A1A2A3A4A5A6A7A8A9AAABACADAEAFB0B1B2B3B4B5B6B7B8B9BABBBCBDBEBF
FIRST_LEN=16
FIRST_ENCRYPTED=E0EC1DF408F551AA6F82C092934970B9
LATER_LEN=16
LATER_ENCRYPTED=9B406066E713C7631157BBF7D89CC550
SM_INIT_LEN=210
SM_INIT_FRAME=D20071247EBD9E5575028AF9A6FB3D2193B3A98D3D89D2753883D251C088F13129AD6C5A586271774A46F072927ECB8F55BBDCE63D4276A1132E68A39C1CA1345E63E0B09256A8B14F281F751464E765791F133B43B9D258379842E2EB921309276B33705A6D41C362DC0A6305D2371839F14CCE4986B6F2B97C7858149AB59148BBA270D7A39761431AD7ABBEC7756AA5531C23CEB8F7481226FBA0F0B2DA25F4A8706E0D428AFBE00E4B8365CC3F9F7BD24B6F379089F57639D1A013FB3411988D805FEEC353FABCEDF8A971F3B9067375
SM_AUTH_GG_PAYLOAD=0B44332211000000000000000000000000000000000050CD00000000000000000B4463EF11000000
SM_LOGIN_OK_PAYLOAD=03E9030000443322110000000000000000EA0300000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
SM_PLAY_OK_PAYLOAD=070403020188776655070000000000000000000000000000
SM_SERVER_LIST_PAYLOAD=040101017F000001611E00000000000064000101000000000200010200000000000000000000000000
SM_GS_CHARACTER_RESPONSE_PAYLOAD=087B000000
SM_REQUEST_KICK_ACCOUNT_PAYLOAD=027B00000001
```
