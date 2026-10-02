using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CmdPalCalendar.Events;

internal static partial class HtmlToMarkdown
{
    private const char LineBreak = '\n';

    public static bool LooksLikeHtml(string text) => HtmlTagRegex().IsMatch(text);

    public static string Convert(string html)
    {
        var writer = new Writer();
        foreach (Match token in TokenRegex().Matches(HiddenBlockRegex().Replace(html, string.Empty)))
        {
            if (token.Groups["name"].Success)
            {
                writer.Tag(token.Groups["name"].Value.ToLowerInvariant(), token.Groups["close"].Success, token.Groups["attrs"].Value);
            }
            else
            {
                writer.Text(WebUtility.HtmlDecode(token.Value));
            }
        }

        return MarkdownText.Tidy(writer.ToString());
    }

    private static string? AllowedHref(string attributes)
    {
        var href = HrefRegex().Match(attributes) is { Success: true } match ? WebUtility.HtmlDecode(match.Groups["href"].Value).Trim() : null;
        return Uri.TryCreate(href, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto" ? href : null;
    }

    private sealed record Span(string Kind, StringBuilder Text, string? Href = null);

    private sealed class Writer
    {
        private readonly Stack<Span> _spans = new();
        private readonly Stack<(bool Ordered, int Count)> _lists = new();
        private readonly StringBuilder _root = new();
        private int _cellIndex;

        private StringBuilder Current => _spans.Count > 0 ? _spans.Peek().Text : _root;

        public void Text(string text) => Current.Append(MarkdownText.Escape(WhitespaceRegex().Replace(text, " ")));

        public void Tag(string name, bool closing, string attributes)
        {
            switch (name)
            {
                case "b" or "strong":
                    Toggle("**", closing);
                    break;
                case "i" or "em":
                    Toggle("*", closing);
                    break;
                case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                    if (closing)
                    {
                        Close("**");
                    }

                    BlankLine();
                    if (!closing)
                    {
                        _spans.Push(new Span("**", new StringBuilder()));
                    }

                    break;
                case "a":
                    if (closing)
                    {
                        Close("a");
                    }
                    else
                    {
                        _spans.Push(new Span("a", new StringBuilder(), AllowedHref(attributes)));
                    }

                    break;
                case "br":
                    Current.Append(LineBreak);
                    break;
                case "p" or "div" or "table" or "blockquote":
                    BlankLine();
                    break;
                case "ul" or "ol":
                    if (closing && _lists.Count > 0)
                    {
                        _lists.Pop();
                    }
                    else if (!closing)
                    {
                        _lists.Push((name == "ol", 0));
                    }

                    NewLine();
                    break;
                case "li" when !closing:
                    NewLine();
                    Current.Append(ListMarker());
                    break;
                case "tr":
                    _cellIndex = 0;
                    NewLine();
                    break;
                case "td" or "th" when !closing:
                    if (_cellIndex++ > 0)
                    {
                        Current.Append(" · ");
                    }

                    break;
            }
        }

        public override string ToString()
        {
            while (_spans.Count > 0)
            {
                Flush(_spans.Pop());
            }

            return _root.ToString();
        }

        private void NewLine()
        {
            if (Current.Length > 0 && !Current.ToString().TrimEnd(' ').EndsWith(LineBreak))
            {
                Current.Append(LineBreak);
            }
        }

        private void BlankLine()
        {
            NewLine();
            Current.Append(LineBreak);
        }

        private void Toggle(string kind, bool closing)
        {
            if (closing)
            {
                Close(kind);
            }
            else
            {
                _spans.Push(new Span(kind, new StringBuilder()));
            }
        }

        private void Close(string kind)
        {
            if (_spans.All(s => s.Kind != kind))
            {
                return;
            }

            Span span;
            do
            {
                span = _spans.Pop();
                Flush(span);
            }
            while (span.Kind != kind);
        }

        private void Flush(Span span)
        {
            var text = span.Text.ToString();
            var core = text.Trim();
            var leading = text[..(text.Length - text.TrimStart().Length)];
            var trailing = text[text.TrimEnd().Length..];

            var rendered = (span.Kind, core.Length) switch
            {
                ("a", _) when span.Href is { } href => $"[{(core.Length > 0 ? core : MarkdownText.Escape(href))}](<{href}>)",
                ("a", _) => core,
                (_, 0) => string.Empty,
                _ => $"{span.Kind}{core}{span.Kind}",
            };

            Current.Append(leading).Append(rendered).Append(trailing);
        }

        private string ListMarker()
        {
            if (_lists.Count == 0)
            {
                return "- ";
            }

            var (ordered, count) = _lists.Pop();
            _lists.Push((ordered, count + 1));
            return ordered ? $"{count + 1}. " : "- ";
        }
    }

    [GeneratedRegex(@"<\s*/?\s*(b|strong|i|em|a|br|p|div|span|ul|ol|li|table|tr|td|h[1-6]|font)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"<(head|style|script)\b[^>]*>.*?</\1\s*>|<!--.*?-->", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HiddenBlockRegex();

    [GeneratedRegex(@"<(?<close>/)?\s*(?<name>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>[^>]*)>|[^<]+|<")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\bhref\s*=\s*(?:""(?<href>[^""]*)""|'(?<href>[^']*)'|(?<href>[^\s>]+))", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();

    [GeneratedRegex(@"[\r\n\t]+")]
    private static partial Regex WhitespaceRegex();
}
