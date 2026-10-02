namespace CmdPalCalendar.Ics;

internal interface ISecretProtector
{
    byte[] Protect(byte[] data);

    byte[] Unprotect(byte[] data);
}
