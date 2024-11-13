using Spectre.Console;

namespace ClashTunBypassTest;

public static class Logging
{
    public static void Debug(string message)
        => AnsiConsole.MarkupLine($"[grey][[debug]] {message}[/]");

    public static void Exception(Exception ex)
        => AnsiConsole.WriteException(ex);
}