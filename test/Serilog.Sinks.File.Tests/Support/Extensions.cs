﻿using Serilog.Events;

namespace Serilog.Sinks.File.Tests.Support;

public static class Extensions
{
    public static object LiteralValue(this LogEventPropertyValue @this)
    {
        return ((ScalarValue)@this).Value!;
    }

    public static List<string> ReadAllLines(this Stream @this)
    {
        var lines = new List<string>();

        using var reader = new StreamReader(@this);
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }
}