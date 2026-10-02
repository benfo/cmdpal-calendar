using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CmdPalCalendar.Ics;

internal sealed record IcsFeedCheckResult(string? CalendarName, string? Error)
{
    public bool IsValid => Error is null;
}

internal sealed class IcsFeedCheck(IcsFeedReader reader)
{
    public async Task<IcsFeedCheckResult> RunAsync(string location, CancellationToken cancellationToken)
    {
        try
        {
            var calendar = await reader.ReadCalendarAsync(location.Trim(), cancellationToken);
            var name = calendar.Properties.Get<string>("X-WR-CALNAME");
            return new IcsFeedCheckResult(string.IsNullOrWhiteSpace(name) ? null : name.Trim(), null);
        }
        catch (ArgumentException)
        {
            return Failed("Enter an https:// or webcal:// address, or the path to an .ics file.");
        }
        catch (HttpRequestException ex)
        {
            return Failed(ex.StatusCode is { } status ? $"Couldn't download the calendar ({(int)status} {status})." : "Couldn't reach that address.");
        }
        catch (OperationCanceledException)
        {
            return Failed("The calendar took too long to download.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failed("Couldn't read that file.");
        }
        catch (InvalidDataException ex)
        {
            return Failed(ex.Message + ".");
        }
        catch (Exception ex)
        {
            return Failed($"Couldn't read that calendar ({ex.Message}).");
        }
    }

    private static IcsFeedCheckResult Failed(string error) => new(null, error);
}
