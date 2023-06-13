using Serilog.Debugging;
using System.Text.RegularExpressions;

namespace Serilog.Sinks.File.GzArchive;

internal static class TokenExpander
{
    private static readonly Regex TokenRegex = new("{(?<Token>(?<Name>[a-zA-Z]+):(?<Format>[^}]+))}", RegexOptions.Compiled | RegexOptions.ECMAScript);
    private static readonly Dictionary<string, Func<string, Token, string>?> Expanders = new()
        {
      {
        "Date",
        (_, token) => DateTime.Now.ToString(token.Format)
      },
      {
        "UtcDate",
        (_, token) => DateTime.UtcNow.ToString(token.Format)
      }
    };

    public static string Expand(string source)
    {
        var startIdx = 0;

        while (TryFindNextToken(source, startIdx, out var token))
        {
            if (Expanders.TryGetValue(token!.Name!, out var func))
            {
                var str = func!(source, token);

                source = source.Remove(token.StartIdx, token.Length);
                source = source.Insert(token.StartIdx, str);
                startIdx = token.StartIdx + str.Length;
            }
            else
            {
                SelfLog.WriteLine("Unsupported token: {0}", token.Name);
                startIdx = token.StartIdx + token.Length;
            }
        }
        return source;
    }

    public static bool IsTokenised(string source) => TryFindNextToken(source, 0, out _);

    private static bool TryFindNextToken(
      string source,
      int startIdx,
      out Token? token)
    {
        var match = TokenRegex.Match(source, startIdx);
        if (!match.Success)
        {
            token = null;
            return false;
        }
        token = new Token
        {
            StartIdx = match.Index,
            Length = match.Length,
            Name = match.Groups["Name"].Value,
            Format = match.Groups["Format"].Value
        };
        return true;
    }

    private class Token
    {
        public int StartIdx { get; set; }

        public int Length { get; set; }

        public string? Name { get; set; }

        public string? Format { get; set; }
    }
}