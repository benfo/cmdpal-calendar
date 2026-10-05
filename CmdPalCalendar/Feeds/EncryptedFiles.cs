using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace CmdPalCalendar.Feeds;

internal sealed record EncryptedFile(byte[] Content, DateTimeOffset WrittenAt);

internal sealed class EncryptedFiles(string directory, string extension, ISecretProtector protector)
{
    public void Write(string id, byte[] content, DateTimeOffset writtenAt)
    {
        Directory.CreateDirectory(directory);
        var path = PathFor(id);
        File.WriteAllBytes(path, protector.Protect(content));
        File.SetLastWriteTimeUtc(path, writtenAt.UtcDateTime);
    }

    public EncryptedFile? Read(string id)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return new EncryptedFile(protector.Unprotect(File.ReadAllBytes(path)), new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero));
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            return null;
        }
    }

    public void RemoveAllExcept(IEnumerable<string> ids)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        var keep = ids.Select(PathFor).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(directory, $"*{extension}").Where(f => !keep.Contains(f)))
        {
            File.Delete(file);
        }
    }

    private string PathFor(string id) => Path.Combine(directory, id + extension);
}
