// Copyright 2023 cocowalla
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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