using System.Security.Cryptography;

namespace BongoCatAutoClaim.Core;

public static class FileHash
{
    public static string Sha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
