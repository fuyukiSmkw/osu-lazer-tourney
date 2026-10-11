// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Components.ChatCommands
{
    /// <summary>
    /// A single command-line option specification: one short form and one long form sharing one value.
    /// </summary>
    public readonly struct OptionSpec
    {
        public readonly char Short;
        public readonly string Long;

        public OptionSpec(char shortName, string longName)
        {
            Short = shortName;
            Long = longName;
        }
    }

    /// <summary>
    /// Minimal command-line parsing infrastructure for the tournament chat commands.
    /// Single and double quotes are fully equivalent: any quote character toggles quoted mode,
    /// spaces outside quotes separate arguments, quote characters themselves are removed.
    /// An explicitly quoted empty string (<c>""</c>) is preserved as an empty argument.
    /// </summary>
    public static class CommandLineParser
    {
        /// <summary>
        /// Splits <paramref name="input"/> into arguments, honouring single and double quotes.
        /// </summary>
        public static List<string> Tokenize(string input)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();
            bool inToken = false;
            bool inQuotes = false;

            foreach (char c in input)
            {
                if (c == '\'' || c == '"')
                {
                    inQuotes = !inQuotes;
                    inToken = true;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (inToken)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        inToken = false;
                    }

                    continue;
                }

                current.Append(c);
                inToken = true;
            }

            if (inToken)
                tokens.Add(current.ToString());

            return tokens;
        }

        /// <summary>
        /// Splits <paramref name="tokens"/> into positional arguments and named options.
        /// Supported forms: <c>--name value</c>, <c>--name=value</c>, <c>-x value</c>, <c>-xvalue</c>.
        /// A lone <c>--</c> stops option parsing; everything after it is positional.
        /// Returns false and sets <paramref name="error"/> when an option is unknown or misses its value.
        /// </summary>
        public static bool TryParseOptions(IReadOnlyList<string> tokens, IReadOnlyList<OptionSpec> specs,
                                           out List<string> positionals, out Dictionary<string, string?> options, out string? error)
        {
            positionals = new List<string>();
            options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            error = null;

            bool onlyPositionals = false;

            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];

                if (onlyPositionals || token.Length < 2 || token[0] != '-')
                {
                    positionals.Add(token);
                    continue;
                }

                if (token == "--")
                {
                    onlyPositionals = true;
                    continue;
                }

                if (token.StartsWith("--", StringComparison.Ordinal))
                {
                    string body = token.Substring(2);
                    string name;
                    string? value;

                    int equals = body.IndexOf('=');

                    if (equals >= 0)
                    {
                        name = body.Substring(0, equals);
                        value = body.Substring(equals + 1);
                    }
                    else
                    {
                        name = body;

                        if (i + 1 >= tokens.Count)
                        {
                            error = $"Option --{name} requires a value.";
                            return false;
                        }

                        value = tokens[++i];
                    }

                    string? canonical = findLong(specs, name);

                    if (canonical == null)
                    {
                        error = $"Unknown option --{name}.";
                        return false;
                    }

                    options[canonical] = value;
                    continue;
                }

                // Short option: -x value or -xvalue.
                char shortName = token[1];
                string? attached = token.Length > 2 ? token.Substring(2) : null;

                if (attached != null && attached.StartsWith("=", StringComparison.Ordinal))
                    attached = attached.Substring(1);

                string? longName = findShort(specs, shortName);

                if (longName == null)
                {
                    error = $"Unknown option -{shortName}.";
                    return false;
                }

                if (attached == null)
                {
                    if (i + 1 >= tokens.Count)
                    {
                        error = $"Option -{shortName} requires a value.";
                        return false;
                    }

                    attached = tokens[++i];
                }

                options[longName] = attached;
            }

            return true;
        }

        private static string? findLong(IReadOnlyList<OptionSpec> specs, string name)
        {
            foreach (var spec in specs)
            {
                if (string.Equals(spec.Long, name, StringComparison.OrdinalIgnoreCase))
                    return spec.Long;
            }

            return null;
        }

        private static string? findShort(IReadOnlyList<OptionSpec> specs, char shortName)
        {
            foreach (var spec in specs)
            {
                if (spec.Short == shortName)
                    return spec.Long;
            }

            return null;
        }
    }
}
