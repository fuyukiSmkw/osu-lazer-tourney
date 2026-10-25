// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Text;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.About
{
    /// <summary>
    /// Minimal inline formatting for guide texts on the About screen.
    /// <c>`code`</c> renders in purple Inter (code wording), <c>**bold**</c> renders bold.
    /// Markers nest (e.g. <c>**defaults to `1`**</c>); unclosed markers apply to the rest of the text.
    /// </summary>
    public static class GuideTextFormatter
    {
        private static readonly Color4 code_colour = new OsuColour().PurpleLight;

        public readonly struct TextSegment
        {
            public string Text { get; }
            public bool Bold { get; }
            public bool Code { get; }

            public TextSegment(string text, bool bold, bool code)
            {
                Text = text;
                Bold = bold;
                Code = code;
            }
        }

        /// <summary>
        /// Splits text into styled segments. Pure logic, safe to unit test headlessly.
        /// </summary>
        public static IReadOnlyList<TextSegment> Parse(string text)
        {
            var segments = new List<TextSegment>();
            bool bold = false;
            bool code = false;
            var current = new StringBuilder();

            void flush()
            {
                if (current.Length == 0)
                    return;

                segments.Add(new TextSegment(current.ToString(), bold, code));
                current.Clear();
            }

            for (int i = 0; i < text.Length;)
            {
                if (text[i] == '*' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    flush();
                    bold = !bold;
                    i += 2;
                }
                else if (text[i] == '`')
                {
                    flush();
                    code = !code;
                    i++;
                }
                else
                {
                    current.Append(text[i]);
                    i++;
                }
            }

            flush();
            return segments;
        }

        public static void AddFormattedText(OsuTextFlowContainer flow, string text, float size = 18)
        {
            foreach (var segment in Parse(text))
            {
                flow.AddText(segment.Text, t =>
                {
                    if (segment.Code)
                    {
                        t.Font = OsuFont.Inter.With(size: size);
                        t.Colour = code_colour;
                    }
                    else if (segment.Bold)
                        t.Font = OsuFont.GetFont(weight: FontWeight.Bold, size: size);
                });
            }
        }
    }
}
