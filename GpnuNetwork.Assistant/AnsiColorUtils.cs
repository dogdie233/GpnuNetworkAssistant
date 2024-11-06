using System.Numerics;

using Spectre.Console;

namespace GpnuNetwork.Assistant;

public static class AnsiColorUtils
{
    public static string Paint<TNum>(this TNum number, TNum div1, Color color1, Color color2)
        where TNum : INumber<TNum>
    {
        var color = number <= div1 ? color1 : color2;
        return $"[{color}]{number}[/]";
    }

    public static string Paint<TNum>(this TNum number, TNum div1, TNum div2, Color color1, Color color2, Color color3)
        where TNum : INumber<TNum>
    {
        Color color;
        if (number <= div1)
            color = color1;
        else if (number <= div2)
            color = color2;
        else
            color = color3;

        return $"[{color}]{number}[/]";
    }
}