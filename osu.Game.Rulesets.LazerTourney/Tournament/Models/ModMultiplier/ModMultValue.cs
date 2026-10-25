// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.LazerTourney.Tournament.Models.ModMultiplier
{
    /// <summary>
    /// The value type of the ModMult rule language.
    /// A value is a number, a boolean, a text or null (e.g. a setting of an absent mod).
    /// </summary>
    public readonly struct ModMultValue
    {
        public enum ValueType
        {
            Number,
            Boolean,
            Text,
            Null,
        }

        public static readonly ModMultValue Null = new ModMultValue(ValueType.Null, 0, false, null);

        public ValueType Type { get; }

        public double Number { get; }

        public bool Boolean { get; }

        public string? Text { get; }

        private ModMultValue(ValueType type, double number, bool boolean, string? text)
        {
            Type = type;
            Number = number;
            Boolean = boolean;
            Text = text;
        }

        public static ModMultValue FromNumber(double number) => new ModMultValue(ValueType.Number, number, false, null);

        public static ModMultValue FromBoolean(bool boolean) => new ModMultValue(ValueType.Boolean, 0, boolean, null);

        public static ModMultValue FromText(string text) => new ModMultValue(ValueType.Text, 0, false, text);

        public bool IsNull => Type == ValueType.Null;

        public bool IsNumber => Type == ValueType.Number;

        /// <summary>
        /// Truthiness used by logical operators and the ternary condition. Null counts as false.
        /// </summary>
        public bool AsCondition() => Type == ValueType.Boolean && Boolean;

        public override string ToString() => Type switch
        {
            ValueType.Number => Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ValueType.Boolean => Boolean ? "true" : "false",
            ValueType.Text => $"\"{Text}\"",
            _ => "null",
        };
    }
}
