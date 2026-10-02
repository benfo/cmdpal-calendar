using System.Security.Cryptography;
using CmdPalCalendar.Feeds;

namespace CmdPalCalendar.Tests;

internal sealed class FailingProtector : ISecretProtector
{
    public byte[] Protect(byte[] data) => data;

    public byte[] Unprotect(byte[] data) => throw new CryptographicException("Wrong user");
}
