using CmdPalCalendar.Ics;

namespace CmdPalCalendar.Tests;

internal sealed class PlainProtector : ISecretProtector
{
    public byte[] Protect(byte[] data) => data;

    public byte[] Unprotect(byte[] data) => data;
}
