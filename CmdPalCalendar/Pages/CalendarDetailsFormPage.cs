using System;
using System.Linq;
using System.Text.Json.Nodes;
using CmdPalCalendar.Feeds;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class CalendarDetailsFormPage : ContentPage
{
    private readonly DetailsForm _form;

    public CalendarDetailsFormPage(CalendarFeedStore store, CalendarFeed feed)
    {
        _form = new DetailsForm(store, feed);
        Name = "Edit";
        Title = $"Edit {feed.Name}";
        Icon = new IconInfo(Glyphs.Edit);
    }

    public override IContent[] GetContent() => [_form];

    private sealed partial class DetailsForm : FormContent
    {
        private readonly CalendarFeedStore _store;
        private readonly CalendarFeed _feed;

        public DetailsForm(CalendarFeedStore store, CalendarFeed feed)
        {
            _store = store;
            _feed = feed;
            TemplateJson = Template();
            DataJson = new JsonObject
            {
                ["from"] = $"From {feed.Source.Label} · {feed.Source.Provider}",
                ["name"] = feed.Name,
                ["color"] = feed.Color.ToString(),
                ["enabled"] = feed.Enabled ? "true" : "false",
            }.ToJsonString();
        }

        public override ICommandResult SubmitForm(string inputs)
        {
            var values = JsonNode.Parse(inputs);
            var name = FormValues.Read(values, "name") is { Length: > 0 } typed ? typed : _feed.Name;
            var color = Enum.TryParse<CalendarColor>(FormValues.Read(values, "color"), out var chosen) ? chosen : _feed.Color;
            var enabled = FormValues.Read(values, "enabled") != "false";

            _store.Update(_store.Current(_feed) with { Name = name, Color = color, Enabled = enabled });
            return CommandResult.ShowToast(new ToastArgs { Message = $"Saved {name}", Result = CommandResult.GoBack() });
        }

        private static string Template()
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
                        "type": "TextBlock",
                        "text": "${from}",
                        "wrap": true,
                        "isSubtle": true
                    },
                    {
                        "type": "Input.Text",
                        "id": "name",
                        "label": "Name",
                        "value": "${name}",
                        "isRequired": true,
                        "errorMessage": "Enter a name"
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
                    }
                ],
                "actions": [
                    {
                        "type": "Action.Submit",
                        "title": "Save"
                    }
                ]
            }
            """;
        }
    }
}
