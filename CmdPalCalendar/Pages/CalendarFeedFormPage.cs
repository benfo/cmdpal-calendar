using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Feeds;
using CmdPalCalendar.Ics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class CalendarFeedFormPage : ContentPage
{
    private readonly CalendarFeedForm _form;

    public CalendarFeedFormPage(CalendarFeedStore store, IcsFeedCheck check, CalendarFeed? feed)
    {
        _form = new CalendarFeedForm(store, check, feed);
        Name = feed is null ? "Add" : "Edit";
        Title = feed is null ? "Add a calendar" : $"Edit {feed.Name}";
        Icon = new IconInfo(feed is null ? Glyphs.Add : Glyphs.Edit);
    }

    public override IContent[] GetContent() => [_form];

    private sealed partial class CalendarFeedForm : FormContent
    {
        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(20);

        private readonly CalendarFeedStore _store;
        private readonly IcsFeedCheck _check;
        private readonly CalendarFeed? _feed;

        public CalendarFeedForm(CalendarFeedStore store, IcsFeedCheck check, CalendarFeed? feed)
        {
            _store = store;
            _check = check;
            _feed = feed;
            TemplateJson = Template(feed is null ? "Add calendar" : "Save");
            Show(feed?.Name ?? string.Empty, feed?.Location ?? string.Empty, feed?.Color ?? store.NextColor, feed?.Enabled ?? true, error: string.Empty);
        }

        public override ICommandResult SubmitForm(string inputs)
        {
            var values = JsonNode.Parse(inputs);
            var name = Value(values, "name");
            var location = Value(values, "location");
            var color = Enum.TryParse<CalendarColor>(Value(values, "color"), out var parsed) ? parsed : _store.NextColor;
            var enabled = Value(values, "enabled") != "false";

            using var timeout = new CancellationTokenSource(CheckTimeout);
            var result = Task.Run(() => _check.RunAsync(location, timeout.Token)).GetAwaiter().GetResult();
            if (!result.IsValid)
            {
                Show(name, location, color, enabled, result.Error!);
                return CommandResult.KeepOpen();
            }

            var finalName = name.Length > 0 ? name : result.CalendarName ?? CalendarFeed.Describe(location);
            if (_feed is null)
            {
                _store.Add(CalendarFeed.Create(finalName, location, color) with { Enabled = enabled });
            }
            else
            {
                _store.Update(_feed with { Name = finalName, Location = location, Color = color, Enabled = enabled });
            }

            var verb = _feed is null ? "Added" : "Saved";
            return CommandResult.ShowToast(new ToastArgs { Message = $"{verb} {finalName}", Result = CommandResult.GoBack() });
        }

        private static string Value(JsonNode? values, string key) => (values?[key]?.GetValue<string>() ?? string.Empty).Trim();

        private void Show(string name, string location, CalendarColor color, bool enabled, string error) =>
            DataJson = new JsonObject
            {
                ["name"] = name,
                ["location"] = location,
                ["color"] = color.ToString(),
                ["enabled"] = enabled ? "true" : "false",
                ["error"] = error,
            }.ToJsonString();

        private static string Template(string submitTitle)
        {
            var colors = new JsonArray(Enum.GetNames<CalendarColor>()
                .Select(c => (JsonNode)new JsonObject { ["title"] = c, ["value"] = c })
                .ToArray());

            return $$"""
            {
                "type": "AdaptiveCard",
                "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                "version": "1.5",
                "body": [
                    {
                        "type": "Input.Text",
                        "id": "location",
                        "label": "Calendar address",
                        "value": "${location}",
                        "placeholder": "https://… or webcal://… or C:\\path\\calendar.ics",
                        "isRequired": true,
                        "errorMessage": "Enter the calendar's address"
                    },
                    {
                        "type": "TextBlock",
                        "text": "In Outlook: Settings > Calendar > Shared calendars > Publish a calendar. In Google: your calendar's settings > Secret address in iCal format.",
                        "wrap": true,
                        "isSubtle": true,
                        "size": "Small"
                    },
                    {
                        "type": "Input.Text",
                        "id": "name",
                        "label": "Name",
                        "value": "${name}",
                        "placeholder": "Leave blank to use the calendar's own name"
                    },
                    {
                        "type": "Input.ChoiceSet",
                        "id": "color",
                        "label": "Colour",
                        "style": "compact",
                        "value": "${color}",
                        "choices": {{colors.ToJsonString()}}
                    },
                    {
                        "type": "Input.Toggle",
                        "id": "enabled",
                        "title": "Show this calendar",
                        "value": "${enabled}",
                        "valueOn": "true",
                        "valueOff": "false"
                    },
                    {
                        "type": "TextBlock",
                        "text": "${error}",
                        "$when": "${error != ''}",
                        "color": "Attention",
                        "wrap": true
                    }
                ],
                "actions": [
                    {
                        "type": "Action.Submit",
                        "title": "{{submitTitle}}"
                    }
                ]
            }
            """;
        }
    }
}
