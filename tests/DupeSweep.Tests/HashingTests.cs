using DupeSweep.Tests.Support;

namespace DupeSweep.Tests;

public class HashingTests
{
    [Fact]
    public void ComputeQuickHash_SameContent_SameHash()
    {
        using var dir = new TempDirectory();
        string a = dir.WriteFile("a.txt", "identical content");
        string b = dir.WriteFile("b.txt", "identical content");

        string hashA = Hashing.ComputeQuickHash(a, new FileInfo(a).Length, CancellationToken.None);
        string hashB = Hashing.ComputeQuickHash(b, new FileInfo(b).Length, CancellationToken.None);

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void ComputeQuickHash_DifferentContent_DifferentHash()
    {
        using var dir = new TempDirectory();
        string a = dir.WriteFile("a.txt", "content one");
        string b = dir.WriteFile("b.txt", "content two");

        string hashA = Hashing.ComputeQuickHash(a, new FileInfo(a).Length, CancellationToken.None);
        string hashB = Hashing.ComputeQuickHash(b, new FileInfo(b).Length, CancellationToken.None);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void ComputeQuickHash_EmptyFile_DoesNotThrow()
    {
        using var dir = new TempDirectory();
        string empty = dir.WriteFile("empty.txt", "");

        string hash = Hashing.ComputeQuickHash(empty, 0, CancellationToken.None);

        Assert.NotEmpty(hash);
    }

    [Fact]
    public async Task ComputeFullHashAsync_SameContent_SameHash()
    {
        using var dir = new TempDirectory();
        string a = dir.WriteFile("a.txt", "identical content");
        string b = dir.WriteFile("b.txt", "identical content");

        string hashA = await Hashing.ComputeFullHashAsync(a, CancellationToken.None);
        string hashB = await Hashing.ComputeFullHashAsync(b, CancellationToken.None);

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public async Task ComputeFullHashAsync_SameSizeDifferentTail_DifferentHash()
    {
        // Same quick-hash sample (both smaller than 64 KB) but different content overall
        // must still resolve to a different full hash.
        using var dir = new TempDirectory();
        string a = dir.WriteFile("a.txt", "shared-prefix-AAAA");
        string b = dir.WriteFile("b.txt", "shared-prefix-BBBB");

        string hashA = await Hashing.ComputeFullHashAsync(a, CancellationToken.None);
        string hashB = await Hashing.ComputeFullHashAsync(b, CancellationToken.None);

        Assert.NotEqual(hashA, hashB);
    }
}
