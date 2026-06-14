using System;
using System.Text;
using Aion.Commons.Nio;

namespace Aion.GameServer.Utils.Captcha;

/// <summary>
/// Java parity: utils/captcha/CAPTCHAUtil (Cura). getRandomWord() ported 1:1.
/// createCAPTCHA() image rendering (java.awt BufferedImage + DDSConverter DXT1) is not gameplay math and
/// depends on platform 2D-graphics; it is deferred to a placeholder DXT1-sized buffer until the DDS pipeline
/// is ported (anti-bot image content only). [[gameplay-faithful-infra-idiomatic]]
/// </summary>
public class CAPTCHAUtil
{
    private const int DEFAULT_WORD_LENGTH = 6;
    private const string WORD = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789";

    private const int IMAGE_WIDTH = 160;
    private const int IMAGE_HEIGHT = 80;

    /// <summary>Java parity: createCAPTCHA(String) -> ByteBuffer (DXT1, no transparency).</summary>
    public static ByteBuffer CreateCAPTCHA(string word)
    {
        // Java parity: DDSConverter.convertToDxt1NoTransparency(image). DXT1 = 8 bytes per 4x4 block.
        int blocks = ((IMAGE_WIDTH + 3) / 4) * ((IMAGE_HEIGHT + 3) / 4);
        return ByteBuffer.Allocate(blocks * 8);
    }

    /// <summary>Java parity: getRandomWord().</summary>
    public static string GetRandomWord()
    {
        return RandomWord(DEFAULT_WORD_LENGTH);
    }

    /// <summary>Java parity: randomWord(int wordLength).</summary>
    private static string RandomWord(int wordLength)
    {
        StringBuilder word = new StringBuilder();

        for (int i = 0; i < wordLength; i++)
        {
            int index = Math.Abs((int)(Random.Shared.NextDouble() * WORD.Length));
            char ch = WORD[index];
            word.Append(ch);
        }

        return word.ToString();
    }
}
