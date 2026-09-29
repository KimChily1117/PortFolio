using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kimchily.Server.Core;

/// <summary>운영자가 로컬 games 폴더에 배치한 불변 번들만 실행한다. 네트워크 업로드 기능은 없다.</summary>
public sealed class ApprovedScriptCatalog(string rootDirectory)
{
    public const int MaximumBundleBytes = 512 * 1024;
    public string RootDirectory { get; } = Path.GetFullPath(rootDirectory);

    public static bool ValidId(string? value) => value is { Length: >= 1 and <= 80 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
    public static bool ValidHash(string? value) => value is { Length: 64 }
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static string Hash(string source) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));

    public ScriptBundle Load(string id, string hash, string worldId)
    {
        // 이름을 먼저 제한하므로 ../ 및 절대 경로를 이용한 카탈로그 탈출이 불가능하다.
        if (!ValidId(id) || !ValidHash(hash) || !ValidId(worldId)) throw new InvalidDataException("Invalid script identity.");
        var path = Path.Combine(RootDirectory, id, hash + ".json");
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaximumBundleBytes) throw new InvalidDataException("Approved script bundle not found or too large.");
        // 디스크 캐시를 무한히 쌓지 않는다. 활성 방마다 자신이 검증한 소스를 보유하며 새 방은 최신 파일을 검사한다.
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > MaximumBundleBytes) throw new InvalidDataException("Script bundle grew beyond the limit.");
        var bundle = JsonSerializer.Deserialize<ScriptBundle>(bytes, Protocol.Json)
            ?? throw new InvalidDataException("Empty script bundle.");
        if (bundle.ScriptId != id || bundle.ScriptHash != hash || bundle.WorldId != worldId
            || bundle.SchemaVersion != 1 || !ValidHash(bundle.SourceHash)
            || bundle.Javascript is not { Length: > 0 and <= 262144 }
            || Hash(bundle.Javascript) != hash)
            throw new InvalidDataException("Script bundle identity, world or content hash mismatch.");
        return bundle;
    }
}

public sealed record ScriptBundle(int SchemaVersion, string ScriptId, string WorldId, string ScriptHash,
    string SourceHash, string CompilerVersion, string Javascript);
