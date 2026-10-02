namespace CmdPalCalendar.Feeds;

internal interface ISecretProtector
{
    byte[] Protect(byte[] data);

    byte[] Unprotect(byte[] data);
}
