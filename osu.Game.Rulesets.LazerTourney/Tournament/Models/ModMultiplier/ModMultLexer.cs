// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models.ModMultiplier
{
    /// <summary>
    /// Thrown for ModMult lexical and syntactic errors. Message is user-facing.
    /// </summary>
    public class ModMultParseException : Exception
    {
        /// <summary>
        /// Zero-based character offset in the source where the error was detected.
        /// </summary>
        public int Position { get; }

        public ModMultParseException(string message, int position)
            : base(position >= 0 ? $"{message} (at position {position})." : message)
        {
            Position = position;
        }
    }

    public enum ModMultTokenType
    {
        Number,
        String,
        Identifier,
        Question,
        Colon,
        Or,
        And,
        Equals,
        NotEquals,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        Plus,
        Minus,
        Asterisk,
        Slash,
        Percent,
        Not,
        OpenParen,
        CloseParen,
        Comma,
        Dot,
        Coalesce,
        End,
    }

    public readonly struct ModMultToken
    {
        public ModMultTokenType Type { get; }
        public string Text { get; }
        public double Number { get; }
        public int Position { get; }

        public ModMultToken(ModMultTokenType type, string text, int position, double number = 0)
        {
            Type = type;
            Text = text;
            Position = position;
            Number = number;
        }
    }

    /// <summary>
    /// Tokeniser for the ModMult rule language (see <see cref="ModMultRule"/> for the language overview).
    /// Supports <c>//</c> and <c>#</c> line comments.
    /// </summary>
    public sealed class ModMultLexer
    {
        private readonly string source;
        private int position;

        public ModMultLexer(string source)
        {
            this.source = source;
        }

        public List<ModMultToken> Tokenise()
        {
            var tokens = new List<ModMultToken>();

            ModMultToken token;
            do
            {
                token = next();
                tokens.Add(token);
            } while (token.Type != ModMultTokenType.End);

            return tokens;
        }

        private ModMultToken next()
        {
            skipTrivia();

            if (position >= source.Length)
                return new ModMultToken(ModMultTokenType.End, string.Empty, position);

            int start = position;
            char c = source[position];

            if (char.IsDigit(c) || (c == '.' && position + 1 < source.Length && char.IsDigit(source[position + 1])))
                return readNumber(start);

            if (c == '"')
                return readString(start);

            if (char.IsLetter(c) || c == '_')
            {
                while (position < source.Length && (char.IsLetterOrDigit(source[position]) || source[position] == '_'))
                    position++;
                return new ModMultToken(ModMultTokenType.Identifier, source.Substring(start, position - start), start);
            }

            position++;

            return (c, position < source.Length ? source[position] : '\0') switch
            {
                ('?', '?') => consume(ModMultTokenType.Coalesce, start, 2),
                ('?', _) => new ModMultToken(ModMultTokenType.Question, "?", start),
                (':', _) => new ModMultToken(ModMultTokenType.Colon, ":", start),
                ('|', '|') => consume(ModMultTokenType.Or, start, 2),
                ('&', '&') => consume(ModMultTokenType.And, start, 2),
                ('=', '=') => consume(ModMultTokenType.Equals, start, 2),
                ('!', '=') => consume(ModMultTokenType.NotEquals, start, 2),
                ('!', _) => new ModMultToken(ModMultTokenType.Not, "!", start),
                ('<', '=') => consume(ModMultTokenType.LessThanOrEqual, start, 2),
                ('<', _) => new ModMultToken(ModMultTokenType.LessThan, "<", start),
                ('>', '=') => consume(ModMultTokenType.GreaterThanOrEqual, start, 2),
                ('>', _) => new ModMultToken(ModMultTokenType.GreaterThan, ">", start),
                ('+', _) => new ModMultToken(ModMultTokenType.Plus, "+", start),
                ('-', _) => new ModMultToken(ModMultTokenType.Minus, "-", start),
                ('*', _) => new ModMultToken(ModMultTokenType.Asterisk, "*", start),
                ('/', _) => new ModMultToken(ModMultTokenType.Slash, "/", start),
                ('%', _) => new ModMultToken(ModMultTokenType.Percent, "%", start),
                ('(', _) => new ModMultToken(ModMultTokenType.OpenParen, "(", start),
                (')', _) => new ModMultToken(ModMultTokenType.CloseParen, ")", start),
                (',', _) => new ModMultToken(ModMultTokenType.Comma, ",", start),
                ('.', _) => new ModMultToken(ModMultTokenType.Dot, ".", start),
                ('=', _) => throw new ModMultParseException("Did you mean \"==\"? Single \"=\" is not an operator", start),
                _ => throw new ModMultParseException($"Unexpected character \"{c}\"", start),
            };
        }

        private ModMultToken consume(ModMultTokenType type, int start, int length)
        {
            position++;
            return new ModMultToken(type, source.Substring(start, length), start);
        }

        private ModMultToken readNumber(int start)
        {
            while (position < source.Length && char.IsDigit(source[position]))
                position++;

            if (position < source.Length && source[position] == '.')
            {
                position++;
                while (position < source.Length && char.IsDigit(source[position]))
                    position++;
            }

            string text = source.Substring(start, position - start);

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new ModMultParseException($"Invalid number \"{text}\"", start);

            return new ModMultToken(ModMultTokenType.Number, text, start, value);
        }

        private ModMultToken readString(int start)
        {
            // Skip the opening quote.
            position++;

            var builder = new StringBuilder();

            while (true)
            {
                if (position >= source.Length)
                    throw new ModMultParseException("Unterminated string literal", start);

                char c = source[position++];

                if (c == '"')
                    break;

                if (c == '\\')
                {
                    if (position >= source.Length)
                        throw new ModMultParseException("Unterminated string literal", start);

                    builder.Append(source[position++] switch
                    {
                        '"' => '"',
                        '\\' => '\\',
                        'n' => '\n',
                        't' => '\t',
                        var e => throw new ModMultParseException($"Unknown escape sequence \"\\{e}\"", position - 2),
                    });
                }
                else
                    builder.Append(c);
            }

            return new ModMultToken(ModMultTokenType.String, builder.ToString(), start);
        }

        private void skipTrivia()
        {
            while (position < source.Length)
            {
                char c = source[position];

                if (char.IsWhiteSpace(c))
                {
                    position++;
                    continue;
                }

                if (c == '#' || (c == '/' && position + 1 < source.Length && source[position + 1] == '/'))
                {
                    while (position < source.Length && source[position] != '\n')
                        position++;
                    continue;
                }

                break;
            }
        }
    }
}
