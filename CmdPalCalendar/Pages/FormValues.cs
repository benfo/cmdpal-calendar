using System.Text.Json.Nodes;

namespace CmdPalCalendar.Pages;

internal static class FormValues
{
    public static string Read(JsonNode? values, string key) => (values?[key]?.GetValue<string>() ?? string.Empty).Trim();
}
