// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Setup
{
    /// <summary>
    /// A numeric text box for TCP ports. The display value is kept as a string because the
    /// official form text box is string-based, while the underlying setting stays an integer
    /// bindable (and therefore persists to the ini as a number).
    /// </summary>
    internal partial class PortTextBox : CompositeDrawable
    {
        public const int MIN_PORT = 1;
        public const int MAX_PORT = 65535;

        /// <summary>
        /// Fired after the valid port has been written to the underlying bindable.
        /// </summary>
        public event Action? ValueChanged;

        private readonly Bindable<int> current;
        private readonly Bindable<string> text;
        private readonly FormNumberBox numberBox;

        public PortTextBox(string caption, Bindable<int> current, string? description = null, bool showRevertToDefaultButton = true)
        {
            // Store a bound copy rather than the model bindable itself. Drawables automatically call
            // UnbindAll() on their IUnbindable fields when disposed, which would otherwise clear every
            // other listener on the shared config bindable.
            this.current = current.GetBoundCopy();

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            int defaultPort = isValidPort(this.current.Default) ? this.current.Default : this.current.Value;

            text = new Bindable<string>(this.current.Value.ToString(CultureInfo.InvariantCulture))
            {
                Default = defaultPort.ToString(CultureInfo.InvariantCulture),
            };

            InternalChild = new SettingsItemV2(numberBox = new FormNumberBox
            {
                Caption = caption,
                HintText = string.IsNullOrEmpty(description) ? default : new LocalisableString(description),
                Current = text,
                LengthLimit = 5,
                SelectAllOnFocus = true,
            })
            {
                ShowRevertToDefaultButton = showRevertToDefaultButton,
            };

            text.ValueChanged += onTextChanged;
            this.current.ValueChanged += onCurrentChanged;

            // Normalise/recover after empty or out-of-range input.
            numberBox.OnCommit += (_, _) =>
            {
                if (!numberBox.ReadOnly)
                    text.Value = this.current.Value.ToString(CultureInfo.InvariantCulture);
            };
        }

        /// <summary>
        /// Locks editing while keeping the current value visible.
        /// </summary>
        /// <remarks>
        /// This must not disable <see cref="text"/>: the inner text box is loaded lazily and its
        /// <c>LoadComplete</c> writes the initial text back into <c>TextBox.Current</c>. A disabled
        /// bindable would make that write throw ("Can not set value ... as bindable is disabled").
        /// </remarks>
        public bool Disabled
        {
            set
            {
                numberBox.ReadOnly = value;

                if (!value)
                    text.Value = current.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void onTextChanged(ValueChangedEvent<string> e)
        {
            if (numberBox.ReadOnly)
                return;

            if (!TryParsePort(e.NewValue, out int port))
                return;

            current.Value = port;
        }

        private void onCurrentChanged(ValueChangedEvent<int> e)
        {
            if (numberBox.ReadOnly)
                return;

            string value = e.NewValue.ToString(CultureInfo.InvariantCulture);

            if (text.Value != value)
                text.Value = value;

            ValueChanged?.Invoke();
        }

        public static bool TryParsePort(string? value, out int port)
            => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out port) && isValidPort(port);

        private static bool isValidPort(int port) => port is >= MIN_PORT and <= MAX_PORT;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                text.ValueChanged -= onTextChanged;
                current.ValueChanged -= onCurrentChanged;
            }
        }
    }
}
