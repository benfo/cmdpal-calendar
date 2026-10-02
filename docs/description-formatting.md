# Description formatting

How an event's description becomes the notes shown in the details pane. The code is in `CmdPalCalendar/Events/EventNotes.cs` and `HtmlToMarkdown.cs`; each rule has a test in `tests/CmdPalCalendar.Tests` (`EventNotesTests`, `HtmlToMarkdownTests`).

## Source

1. Outlook's HTML version (`X-ALT-DESC;FMTTYPE=text/html`) when the event has one.
2. Otherwise `DESCRIPTION`. Google feeds put HTML straight in here, so it is converted if it contains HTML tags; plain text is only escaped, keeping its line breaks.

The original `DESCRIPTION` is kept on the event as well, because meeting-link and meeting ID detection need the full text, including the invite block.

## HTML to Markdown

| HTML | Shown as |
|---|---|
| `<b>`, `<strong>` | **bold** |
| `<i>`, `<em>` | *italic* |
| `<h1>`–`<h6>` | a bold line (headings would be too large in the details pane) |
| `<a href>` | a link, if the scheme is `http`, `https` or `mailto`; otherwise just the text |
| `<br>` | a line break |
| `<p>`, `<div>`, `<table>`, `<blockquote>` | a paragraph break |
| `<ul>`, `<ol>`, `<li>` | `- item` or `1. item`; nested lists are flattened to one level |
| `<tr>`, `<td>`, `<th>` | one line per row, cells joined with ` · ` |
| `<img>` | dropped |
| `<head>`, `<style>`, `<script>`, comments | dropped with their content |
| any other tag (`<span>`, `<font>`, `<u>`, …) | dropped, text kept |

Text is HTML-decoded (`&amp;`, `&nbsp;`) and Markdown-escaped, so stray `*` or `#` don't change the layout. Newlines in the HTML source count as spaces; runs of spaces collapse to one, and runs of blank lines to one.

## Invite block

Everything from the first line of a Teams or Zoom invite block onwards is cut: a line of underscores (Teams' separator), "Microsoft Teams meeting", "Microsoft Teams Need help?" or "Join Zoom Meeting". Join, Copy link and Copy meeting ID and passcode already cover what's in it. There is no length limit otherwise; the details pane scrolls.

## Decisions

- **Links keep their original address, Safe Links included.** Unwrapping would skip Microsoft's click-time check, which can block a page that turned malicious after the invite was sent. The link text looks the same either way. Join links are different: they are unwrapped, because the service has to be recognised and Teams rejects some rewritten links.
- **Images are dropped.** Showing a remote image would tell the sender when and where the event was opened (tracking pixels), and invite images are rarely useful. Revisit if embedded (`data:`) images turn out to matter.
- **Tables are flattened, not dropped.** Outlook and Teams use tables for layout, so real Markdown tables would mostly be empty grids, but the content can still matter.
