using System.Security.Cryptography;

namespace DupeSweep;

/// <summary>Two-tier hashing: a cheap sample hash to narrow candidates, then a full hash to confirm.</summary>
public static class Hashing
{
    private const int QuickHashSampleBytes = 64 * 1024;

    /// <summary>Hashes up to the first 64 KB of the file. Cheap enough to run on every same-size candidate.</summary>
    public static string ComputeQuickHash(string path, long fileLength, CancellationToken ct)
    {
        using SHA256 sha256 = SHA256.Create();
        using FileStream stream = File.OpenRead(path);

        int sampleSize = (int)Math.Min(fileLength, QuickHashSampleBytes);
        if (sampleSize == 0)
            return Convert.ToHexString(sha256.ComputeHash([]));

        byte[] buffer = new byte[sampleSize];
        int read = ReadFully(stream, buffer, ct);
        return Convert.ToHexString(sha256.ComputeHash(buffer, 0, read));
    }

    /// <summary>Hashes the entire file. Only run on files whose quick hash already collided.</summary>
    public static async Task<string> ComputeFullHashAsync(string path, CancellationToken ct)
    {
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static int ReadFully(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            ct.ThrowIfCancellationRequested();
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0) break;
            total += read;
        }
        return total;
    }
}
