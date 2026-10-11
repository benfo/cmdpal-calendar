using System;
using System.Net.Http;
using Microsoft.Graph.Models.ODataErrors;

namespace CmdPalCalendar.Graph;

internal static class GraphErrors
{
    public static string Describe(Exception exception) => exception switch
    {
        SignInRequiredException => "Sign in again to see these calendars",
        ODataError { Error.Message: { Length: > 0 } message } => message,
        HttpRequestException => "Couldn't reach Microsoft. Check your connection.",
        _ => exception.Message,
    };
}
