using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace CmdPalCalendar;

[Guid("024DB1BD-FAE8-45C6-8BE7-871E23F018BB")]
public sealed partial class CalendarExtension(ManualResetEvent disposed) : IExtension, IDisposable
{
    private readonly CalendarCommandProvider _provider = new();

    public object? GetProvider(ProviderType providerType) =>
        providerType == ProviderType.Commands ? _provider : null;

    public void Dispose() => disposed.Set();
}
