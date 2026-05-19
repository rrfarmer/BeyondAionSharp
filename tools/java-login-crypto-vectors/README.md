# Java Login Crypto Vectors

This helper generates login crypto reference bytes from the original Java
`BlowfishCipher` and `CryptEngine` sources. It exists so the C# port can keep
byte-for-byte tests without requiring a JDK on the host machine.

Run from the repository root:

```powershell
$repo = (Get-Location).Path
docker run --rm -v "${repo}:/work" -w /work eclipse-temurin:8-jdk bash -lc "mkdir -p /tmp/aion-vectors/classes && javac -d /tmp/aion-vectors/classes login-server/src/com/aionemu/loginserver/network/ncrypt/BlowfishCipher.java login-server/src/com/aionemu/loginserver/network/ncrypt/CryptEngine.java dotnetConversion/tools/java-login-crypto-vectors/com/aionemu/commons/utils/Rnd.java dotnetConversion/tools/java-login-crypto-vectors/VectorGenerator.java && java -cp /tmp/aion-vectors/classes VectorGenerator"
```

Current output:

```text
BLOWFISH_STATIC_0_15=458EF8CB40966A791B9161DBC9042822
FIRST_LEN=16
FIRST_ENCRYPTED=E0EC1DF408F551AA6F82C092934970B9
LATER_LEN=16
LATER_ENCRYPTED=9B406066E713C7631157BBF7D89CC550
```
