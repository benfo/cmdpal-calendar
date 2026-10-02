using CmdPalCalendar.Events;

namespace CmdPalCalendar.Tests;

public sealed class HtmlToMarkdownTests
{
    [Theory]
    [InlineData("<b>Agenda</b> for today", "**Agenda** for today")]
    [InlineData("<strong>Agenda</strong>", "**Agenda**")]
    [InlineData("Please <i>read</i> first", "Please *read* first")]
    [InlineData("<em>Note</em>", "*Note*")]
    [InlineData("Say<b> hello </b>there", "Say **hello** there")]
    [InlineData("<b></b>Empty", "Empty")]
    public void Converts_emphasis(string html, string markdown)
    {
        Assert.Equal(markdown, HtmlToMarkdown.Convert(html));
    }

    [Fact]
    public void Converts_line_breaks_and_paragraphs()
    {
        Assert.Equal("One  \nTwo\n\nThree", HtmlToMarkdown.Convert("One<br>Two<p>Three</p>"));
    }

    [Fact]
    public void Treats_source_newlines_as_spaces()
    {
        Assert.Equal("Join the call now", HtmlToMarkdown.Convert("Join the\r\n    call\tnow"));
    }

    [Fact]
    public void Collapses_blank_lines_and_spaces()
    {
        Assert.Equal("One\n\nTwo", HtmlToMarkdown.Convert("<p>One</p><p></p><div>  </div><p>  Two&nbsp;&nbsp;</p>"));
    }

    [Theory]
    [InlineData("<a href=\"https://example.com/agenda\">Agenda</a>", "[Agenda](<https://example.com/agenda>)")]
    [InlineData("<a href='mailto:ben@example.com'>Ben</a>", "[Ben](<mailto:ben@example.com>)")]
    [InlineData("<a href=\"https://example.com/x\"></a>", "[https://example.com/x](<https://example.com/x>)")]
    [InlineData("<a href=\"javascript:alert(1)\">Click</a>", "Click")]
    [InlineData("<a href=\"file:///c:/secret.txt\">File</a>", "File")]
    [InlineData("<a>No link</a>", "No link")]
    public void Keeps_only_web_and_mail_links(string html, string markdown)
    {
        Assert.Equal(markdown, HtmlToMarkdown.Convert(html));
    }

    [Fact]
    public void Keeps_safe_links_wrapping_as_written()
    {
        const string wrapped = "https://eur02.safelinks.protection.outlook.com/?url=https%3A%2F%2Fexample.com&data=05";

        Assert.Equal($"[Doc](<{wrapped}>)", HtmlToMarkdown.Convert($"<a href=\"{wrapped.Replace("&", "&amp;", StringComparison.Ordinal)}\">Doc</a>"));
    }

    [Fact]
    public void Drops_images()
    {
        Assert.Equal("Hello", HtmlToMarkdown.Convert("<img src=\"https://tracker.example.com/p.gif\">Hello"));
    }

    [Fact]
    public void Flattens_tables_into_lines()
    {
        const string html = "<table><tr><td>09:00</td><td>Intro</td><td>Ben</td></tr><tr><td>09:30</td><td>Demo</td></tr></table>";

        Assert.Equal("09:00 · Intro · Ben  \n09:30 · Demo", HtmlToMarkdown.Convert(html));
    }

    [Fact]
    public void Turns_headings_into_bold_lines()
    {
        Assert.Equal("**Agenda**\n\nItems", HtmlToMarkdown.Convert("<h2>Agenda</h2>Items"));
    }

    [Fact]
    public void Converts_lists_and_flattens_nesting()
    {
        const string html = "<ul><li>One</li><li>Two<ol><li>Nested</li></ol></li></ul><ol><li>First</li><li>Second</li></ol>";

        Assert.Equal("- One  \n- Two  \n1. Nested  \n1. First  \n2. Second", HtmlToMarkdown.Convert(html));
    }

    [Fact]
    public void Drops_unknown_tags_but_keeps_their_text()
    {
        Assert.Equal("Red text", HtmlToMarkdown.Convert("<span style=\"color:red\"><font face=\"Arial\">Red</font> <u>text</u></span>"));
    }

    [Fact]
    public void Drops_head_style_script_and_comments_with_their_content()
    {
        const string html = "<html><head><title>T</title><style>p{color:red}</style></head><body><!-- hi --><script>alert(1)</script><p>Body</p></body></html>";

        Assert.Equal("Body", HtmlToMarkdown.Convert(html));
    }

    [Fact]
    public void Escapes_markdown_in_text_and_decodes_entities()
    {
        Assert.Equal("5 \\* 3 \\< 20 \\#1", HtmlToMarkdown.Convert("5 * 3 &lt; 20 #1"));
    }

    [Theory]
    [InlineData("<b>Agenda</b>", true)]
    [InlineData("Line one<br/>Line two", true)]
    [InlineData("Plain text with 3 < 5 and a > b", false)]
    [InlineData("Email <ben@example.com>", false)]
    public void Detects_html(string text, bool expected)
    {
        Assert.Equal(expected, HtmlToMarkdown.LooksLikeHtml(text));
    }
}
