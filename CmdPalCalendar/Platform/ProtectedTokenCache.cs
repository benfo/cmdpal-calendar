using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using CmdPalCalendar.Feeds;
using Microsoft.Identity.Client;

namespace CmdPalCalendar.Platform;

internal sealed class ProtectedTokenCache(string path, ISecretProtector protector)
{
    private readonly Lock _lock = new();

    public void Bind(ITokenCache cache)
    {
        cache.SetBeforeAccess(Load);
        cache.SetAfterAccess(Save);
    }

    private void Load(TokenCacheNotificationArgs args)
    {
        lock (_lock)
        {
            args.TokenCache.DeserializeMsalV3(Read(), shouldClearExistingCache: true);
        }
    }

    private void Save(TokenCacheNotificationArgs args)
    {
        if (!args.HasStateChanged)
        {
            return;
        }

        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, protector.Protect(args.TokenCache.SerializeMsalV3()));
        }
    }

    private byte[]? Read()
    {
        try
        {
            return File.Exists(path) ? protector.Unprotect(File.ReadAllBytes(path)) : null;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            return null;
        }
    }
}
