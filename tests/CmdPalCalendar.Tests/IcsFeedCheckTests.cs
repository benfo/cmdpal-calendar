using CmdPalCalendar.Ics;

namespace CmdPalCalendar.Tests;

public sealed class IcsFeedCheckTests : IDisposable
{
    private readonly List<string> _files = [];
    private readonly IcsFeedCheck _check = new(new IcsFeedReader());

    [Fact]
    public async Task Accepts_a_calendar_and_reads_its_name()
    {
        var path = WriteFile("BEGIN:VCALENDAR\nVERSION:2.0\nPRODID:-//t//EN\nX-WR-CALNAME:Family\nEND:VCALENDAR\n");

        var result = await _check.RunAsync(path, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("Family", result.CalendarName);
    }

    [Fact]
    public async Task Accepts_a_calendar_without_a_name()
    {
        var result = await _check.RunAsync(WriteFile("BEGIN:VCALENDAR\nVERSION:2.0\nPRODID:-//t//EN\nEND:VCALENDAR\n"), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.CalendarName);
    }

    [Fact]
    public async Task Rejects_a_file_that_is_not_a_calendar()
    {
        var result = await _check.RunAsync(WriteFile("<html><body>Sign in</body></html>"), CancellationToken.None);

        Assert.Equal("That isn't an iCalendar (.ics) feed.", result.Error);
    }

    [Theory]
    [InlineData("not a url or file")]
    [InlineData("")]
    public async Task Rejects_something_that_is_not_an_address(string location)
    {
        var result = await _check.RunAsync(location, CancellationToken.None);

        Assert.Equal("Enter an https:// or webcal:// address, or the path to an .ics file.", result.Error);
    }

    public void Dispose()
    {
        foreach (var file in _files)
        {
            File.Delete(file);
        }
    }

    private string WriteFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ics");
        File.WriteAllText(path, content);
        _files.Add(path);
        return path;
    }
}
