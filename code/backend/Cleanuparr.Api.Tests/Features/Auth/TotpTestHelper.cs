using System.Security.Cryptography;

namespace Cleanuparr.Api.Tests.Features.Auth;

internal static class TotpTestHelper
{
    public static string GenerateTotpCode(string base32Secret)
    {
        var key = Base32Decode(base32Secret);
        var timestep = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds / 30;
        var timestepBytes = BitConverter.GetBytes(timestep);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(timestepBytes);
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(timestepBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        return (binaryCode % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        base32 = base32.ToUpperInvariant().TrimEnd('=');

        var bits = new List<byte>();
        foreach (var c in base32)
        {
            var val = alphabet.IndexOf(c);
            if (val < 0)
            {
                continue;
            }

            for (var i = 4; i >= 0; i--)
            {
                bits.Add((byte)((val >> i) & 1));
            }
        }

        var bytes = new byte[bits.Count / 8];
        for (var i = 0; i < bytes.Length; i++)
        {
            for (var j = 0; j < 8; j++)
            {
                bytes[i] = (byte)((bytes[i] << 1) | bits[i * 8 + j]);
            }
        }

        return bytes;
    }
}
