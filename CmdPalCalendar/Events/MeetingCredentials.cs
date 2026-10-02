using System.Linq;
using System.Text.RegularExpressions;

namespace CmdPalCalendar.Events;

internal sealed partial record MeetingCredentials(string? MeetingId, string? Passcode)
{
    public static MeetingCredentials? Find(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var id = MeetingIdRegex().Match(text) is { Success: true } idMatch ? WhitespaceRegex().Replace(idMatch.Groups["id"].Value.Trim(), " ") : null;
        var passcode = PasscodeRegex().Match(text) is { Success: true } passMatch ? passMatch.Groups["code"].Value : null;

        return id is null && passcode is null ? null : new MeetingCredentials(id, passcode);
    }

    public override string ToString() =>
        string.Join(" · ", new[] { MeetingId is null ? null : $"Meeting ID: {MeetingId}", Passcode is null ? null : $"Passcode: {Passcode}" }.OfType<string>());

    [GeneratedRegex(@"\bMeeting\s*ID\s*[:：]?\s*(?<id>\d{3}(?:[ \t-]?\d{3,4}){2,4}(?:[ \t-]?\d{1,2}\b)?)", RegexOptions.IgnoreCase)]
    private static partial Regex MeetingIdRegex();

    [GeneratedRegex(@"\b(?:Passcode|Password|Pass\s*code)\s*[:：]?\s*(?<code>[A-Za-z0-9]{4,12})\b", RegexOptions.IgnoreCase)]
    private static partial Regex PasscodeRegex();

    [GeneratedRegex(@"[ \t-]+")]
    private static partial Regex WhitespaceRegex();
}
