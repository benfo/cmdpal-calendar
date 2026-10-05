using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CmdPalCalendar.Graph;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalCalendar.Pages;

internal sealed partial class MicrosoftCalendarsPage : ContentPage
{
    private readonly MicrosoftCalendarChoice _choice;
    private readonly ChoiceForm _form;
    private int _opened;

    public MicrosoftCalendarsPage(MicrosoftCalendarChoice choice)
    {
        _choice = choice;
        _form = new ChoiceForm(choice);
        _choice.Changed += (_, _) => Refresh();

        Name = "Choose";
        Title = "Choose calendars";
        Icon = new IconInfo(Glyphs.Calendar);
    }

    public override IContent[] GetContent()
    {
        if (Interlocked.Exchange(ref _opened, 1) == 0)
        {
            _ = Task.Run(() => _choice.OpenAsync(CancellationToken.None));
        }

        Refresh();
        return [_form];
    }

    private void Refresh()
    {
        IsLoading = _choice.IsBusy;
        _form.Show();
    }

    private sealed partial class ChoiceForm(MicrosoftCalendarChoice choice) : FormContent
    {
        private int _saving;

        public void Show()
        {
            TemplateJson = choice.Calendars is { } calendars && !choice.IsBusy ? CalendarsCard(calendars) : StatusCard();
            DataJson = "{}";
        }

        public override ICommandResult SubmitForm(string inputs, string data)
        {
            if (JsonNode.Parse(data)?["action"]?.GetValue<string>() == "retry")
            {
                _ = Task.Run(() => choice.RetryAsync(CancellationToken.None));
                return CommandResult.KeepOpen();
            }

            if (Interlocked.Exchange(ref _saving, 1) == 1)
            {
                return CommandResult.KeepOpen();
            }

            try
            {
                return Save(Selected(inputs));
            }
            finally
            {
                Volatile.Write(ref _saving, 0);
            }
        }

        private CommandResult Save(HashSet<string> selected)
        {
            var account = choice.Account?.Username ?? "this account";
            if (selected.Count == 0)
            {
                return CommandResult.Confirm(new ConfirmationArgs
                {
                    Title = $"Sign out of {account}?",
                    Description = "No calendars are selected, so you'll be signed out. You can add it again later.",
                    PrimaryCommand = new AnonymousCommand(() => _ = choice.SignOutAsync(CancellationToken.None)) { Name = "Sign out", Result = CommandResult.GoBack() },
                    IsPrimaryCommandCritical = true,
                });
            }

            var saved = choice.Save(selected);
            var message = (saved.Added, saved.Removed) switch
            {
                (0, 0) => "No changes",
                (var added, 0) => $"Added {Calendars(added)}",
                (0, var removed) => $"Removed {Calendars(removed)}",
                (var added, var removed) => $"Added {Calendars(added)}, removed {Calendars(removed)}",
            };
            return CommandResult.ShowToast(new ToastArgs { Message = message, Result = CommandResult.GoBack() });
        }

        private HashSet<string> Selected(string inputs)
        {
            var values = JsonNode.Parse(inputs);
            return (choice.Calendars ?? [])
                .Where((_, i) => values?[$"c{i}"]?.GetValue<string>() == "true")
                .Select(c => c.Id)
                .ToHashSet();
        }

        private static string Calendars(int count) => count == 1 ? "1 calendar" : $"{count} calendars";

        private string CalendarsCard(IReadOnlyList<GraphCalendar> calendars)
        {
            var toggles = calendars.Select((c, i) => (JsonNode)new JsonObject
            {
                ["type"] = "Input.Toggle",
                ["id"] = $"c{i}",
                ["title"] = Label(c),
                ["value"] = choice.IsAdded(c) ? "true" : "false",
                ["valueOn"] = "true",
                ["valueOff"] = "false",
            });
            JsonNode[] intro = [Text($"Choose which calendars from {choice.Account?.Username} to show.", subtle: true)];
            JsonNode[] problem = choice.Problem is { } p ? [Problem(p)] : [];
            return Card([.. intro, .. toggles, .. problem], Action("Save", "save"));
        }

        private string StatusCard()
        {
            if (choice.IsBusy || choice.Problem is not { } problem)
            {
                var waiting = choice.Account is null ? "Signing in… Choose an account in the Windows sign-in window." : "Loading your calendars…";
                return Card([Text(waiting)], null);
            }

            return Card([Text("Couldn't show your calendars."), Problem(problem)], Action(choice.NeedsSignInAgain ? "Sign in again" : "Try again", "retry"));
        }

        private static string Label(GraphCalendar calendar) =>
            calendar.IsDefault ? $"{calendar.Name} · your default calendar"
            : calendar.SharedBy is { } owner ? $"{calendar.Name} · shared by {owner}"
            : calendar.Name;

        private static JsonObject Text(string text, bool subtle = false) =>
            new() { ["type"] = "TextBlock", ["text"] = text, ["wrap"] = true, ["isSubtle"] = subtle };

        private static JsonObject Problem(string text) =>
            new() { ["type"] = "TextBlock", ["text"] = text, ["wrap"] = true, ["color"] = "Attention" };

        private static JsonObject Action(string title, string action) =>
            new() { ["type"] = "Action.Submit", ["title"] = title, ["data"] = new JsonObject { ["action"] = action } };

        private static string Card(JsonNode[] body, JsonObject? action) =>
            new JsonObject
            {
                ["type"] = "AdaptiveCard",
                ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
                ["version"] = "1.5",
                ["body"] = new JsonArray(body),
                ["actions"] = action is null ? new JsonArray() : new JsonArray(action),
            }.ToJsonString();
    }
}
