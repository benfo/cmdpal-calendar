using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Feeds;
using CmdPalCalendar.Ics;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class IcsAddressFormPage : ContentPage
{
    private readonly AddressForm _form;

    public IcsAddressFormPage(CalendarFeedStore store, IcsFeedCheck check, CalendarFeed? feed)
    {
        _form = new AddressForm(store, check, feed, busy => IsLoading = busy);
        Name = feed is null ? "Add" : "Change address…";
        Title = feed is null ? "Add a calendar address or file" : $"Change the address of {feed.Name}";
        Icon = new IconInfo(feed is null ? Glyphs.Add : Glyphs.Link);
    }

    public override IContent[] GetContent() => [_form];

    private sealed partial class AddressForm : FormContent
    {
        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(20);

        private readonly CalendarFeedStore _store;
        private readonly IcsFeedCheck _check;
        private readonly CalendarFeed? _feed;
        private readonly Action<bool> _setBusy;
        private int _checking;

        public AddressForm(CalendarFeedStore store, IcsFeedCheck check, CalendarFeed? feed, Action<bool> setBusy)
        {
            _store = store;
            _check = check;
            _feed = feed;
            _setBusy = setBusy;
            TemplateJson = Template(feed is null ? "Add calendar" : "Save");
            Show((feed?.Source as IcsSource)?.Location ?? string.Empty);
        }

        public override ICommandResult SubmitForm(string inputs)
        {
            if (Interlocked.Exchange(ref _checking, 1) == 1)
            {
                return CommandResult.KeepOpen();
            }

            try
            {
                return Save(FormValues.Read(JsonNode.Parse(inputs), "location"));
            }
            finally
            {
                Volatile.Write(ref _checking, 0);
            }
        }

        private CommandResult Save(string location)
        {
            if (_feed is { Source: IcsSource current } && string.Equals(current.Location, location, StringComparison.Ordinal))
            {
                return CommandResult.GoBack();
            }

            var result = Check(location);
            if (!result.IsValid)
            {
                Show(location, error: result.Error!);
                return CommandResult.KeepOpen();
            }

            if (_feed is null)
            {
                var name = result.CalendarName ?? IcsSource.Describe(location);
                _store.Add(CalendarFeed.Create(name, new IcsSource(location), _store.NextColor));
                return CommandResult.ShowToast(new ToastArgs { Message = $"Added {name}", Result = CommandResult.GoBack() });
            }

            _store.Update(_store.Current(_feed) with { Source = new IcsSource(location) });
            return CommandResult.ShowToast(new ToastArgs { Message = $"Saved {_feed.Name}", Result = CommandResult.GoBack() });
        }

        private IcsFeedCheckResult Check(string location)
        {
            Show(location, status: "Checking the calendar…");
            _setBusy(true);
            try
            {
                using var timeout = new CancellationTokenSource(CheckTimeout);
                return Task.Run(() => _check.RunAsync(location, timeout.Token)).GetAwaiter().GetResult();
            }
            finally
            {
                _setBusy(false);
                Show(location);
            }
        }

        private void Show(string location, string error = "", string status = "") =>
            DataJson = new JsonObject
            {
                ["location"] = location,
                ["error"] = error,
                ["status"] = status,
            }.ToJsonString();

        private static string Template(string submitTitle) =>
            $$"""
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
                        "type": "TextBlock",
                        "text": "${status}",
                        "$when": "${status != ''}",
                        "weight": "Bolder",
                        "wrap": true
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
