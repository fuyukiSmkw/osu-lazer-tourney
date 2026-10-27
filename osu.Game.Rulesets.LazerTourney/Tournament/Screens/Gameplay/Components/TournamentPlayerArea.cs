// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.LazerTourney.Tournament.Components;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.OnlinePlay.Multiplayer.Spectate;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Screens.Gameplay.Components
{
    /// <summary>
    /// Tournament copy of <see cref="PlayerArea"/>.
    /// Provides an area for and manages the hierarchy of a spectated player within a <see cref="SpectateCell"/>.
    /// </summary>
    /// <remarks>
    /// Forked so gameplay is driven by <see cref="TournamentSpectatorPlayer"/> (without the official
    /// username label) instead of <see cref="MultiSpectatorPlayer"/>, and pushed through
    /// <see cref="TournamentSpectatorPlayerLoader"/> so the loader cannot touch the shared track
    /// (<see cref="PlayerLoader"/> muffles and stops it; other cells keep playing the master clock).
    /// Everything else mirrors official.
    /// </remarks>
    public partial class TournamentPlayerArea : CompositeDrawable
    {
        /// <summary>
        /// Raised after <see cref="Player.StartGameplay"/> is called on <see cref="Player"/>.
        /// </summary>
        public event Action? OnGameplayStarted;

        /// <summary>
        /// Whether a <see cref="Player"/> is loaded in the area.
        /// </summary>
        public bool PlayerLoaded => stack?.CurrentScreen is Player { IsLoaded: true };

        /// <summary>
        /// The user id this <see cref="TournamentPlayerArea"/> corresponds to.
        /// </summary>
        public readonly int UserId;

        /// <summary>
        /// The <see cref="SpectatorPlayerClock"/> used to control the gameplay running state of a loaded <see cref="Player"/>.
        /// </summary>
        public readonly SpectatorPlayerClock SpectatorPlayerClock;

        /// <summary>
        /// The clock adjustments applied by the <see cref="Player"/> loaded in this area.
        /// </summary>
        public IAggregateAudioAdjustment ClockAdjustmentsFromMods => clockAdjustmentsFromMods;

        /// <summary>
        /// The currently-loaded score.
        /// </summary>
        public Score? Score { get; private set; }

        /// <summary>
        /// The score processor of the loaded player, if available.
        /// </summary>
        public ScoreProcessor? ScoreProcessor { get; private set; }

        /// <summary>
        /// The health processor of the loaded player, if available.
        /// </summary>
        public HealthProcessor? HealthProcessorPublic { get; private set; }

        /// <summary>
        /// The gameplay state of the loaded player (parsed playable beatmap + mods), if available.
        /// Captured alongside <see cref="ScoreProcessor"/> when gameplay starts.
        /// </summary>
        public GameplayState? GameplayStatePublic { get; private set; }

        /// <summary>
        /// Per-score custom multiplier for the loaded score, applied to the cell HUD and results.
        /// Set before <see cref="LoadScore"/>; 1 means no adjustment.
        /// </summary>
        public double ScoreWeight { get; set; } = 1;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        private readonly AudioAdjustments clockAdjustmentsFromMods = new AudioAdjustments();
        private readonly BindableDouble volumeAdjustment = new BindableDouble();
        private readonly IAggregateAudioAdjustment? masterAdjustments;
        private readonly Container gameplayContent;
        private readonly LoadingLayer loadingLayer;
        private OsuScreenStack? stack;

        public TournamentPlayerArea(int userId, SpectatorPlayerClock clock, IAggregateAudioAdjustment? masterAdjustments = null)
        {
            UserId = userId;
            SpectatorPlayerClock = clock;
            this.masterAdjustments = masterAdjustments;

            RelativeSizeAxes = Axes.Both;

            AudioContainer audioContainer;
            InternalChildren = new Drawable[]
            {
                audioContainer = new AudioContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = gameplayContent = new DrawSizePreservingFillContainer { RelativeSizeAxes = Axes.Both },
                },
                loadingLayer = new LoadingLayer(true) { State = { Value = Visibility.Visible } }
            };

            audioContainer.AddAdjustment(AdjustableProperty.Volume, volumeAdjustment);
        }

        public void LoadScore(Score score, bool suppressAudioEffects = false)
        {
            if (Score != null)
                throw new InvalidOperationException($"Cannot load a new score on a {nameof(TournamentPlayerArea)} that has an existing score.");

            Score = score;

            // Required for freestyle, where each player may be playing a different beatmap.
            var workingBeatmap = beatmapManager.GetWorkingBeatmap(Score.ScoreInfo.BeatmapInfo);

            // Required to avoid crashes, but we really don't want to be doing this if we can avoid it.
            // If we get to fixing this, we will want to investigate every access to `Track` in gameplay.
            // The loaded track is intentionally kept alive by the shared working beatmap cache.
            // Disposing it here would poison every other cell and the master clock that share
            // the same cached track instance, because TrackLoaded stays true afterwards.
            if (!workingBeatmap.TrackLoaded)
                workingBeatmap.LoadTrack();

            gameplayContent.Child = new PlayerIsolationContainer(workingBeatmap, Score.ScoreInfo.Ruleset, Score.ScoreInfo.Mods)
            {
                RelativeSizeAxes = Axes.Both,
                Child = stack = new OsuScreenStack
                {
                    Name = nameof(TournamentPlayerArea),
                }
            };

            // Single-player reset (suppressAudioEffects=true) uses our fork that neutralises
            // PlayerLoader's track mute/stop + entrance filters. Normal start / full reset use
            // the official loader to preserve the entrance blur/fade behaviour.
            SpectatorPlayerLoader loader = suppressAudioEffects
                ? new TournamentSpectatorPlayerLoader(Score, () => createPlayer(), masterAdjustments)
                : new MultiSpectatorPlayerLoader(Score, () => createPlayer());

            stack.Push(loader);

            loadingLayer.Hide();

            TournamentSpectatorPlayer createPlayer()
            {
                var player = new TournamentSpectatorPlayer(Score, SpectatorPlayerClock)
                {
                    ScoreWeight = ScoreWeight,
                };
                player.OnGameplayStarted += () =>
                {
                    ScoreProcessor = player.ScoreProcessorPublic;
                    HealthProcessorPublic = player.HealthProcessorPublic;
                    GameplayStatePublic = player.GameplayStatePublic;
                    OnGameplayStarted?.Invoke();
                };

                clockAdjustmentsFromMods.BindAdjustments(player.ClockAdjustmentsFromMods);

                return player;
            }
        }

        public bool Mute
        {
            get;
            set
            {
                if (field == value)
                    return;

                field = value;
                volumeAdjustment.Value = value ? 0 : 1;
                Logger.Log($"{(field ? "muting" : "unmuting")} player {UserId}");
            }
        } = true;

        // Player interferes with global input, so disable input for now.
        public override bool PropagatePositionalInputSubTree => false;
        public override bool PropagateNonPositionalInputSubTree => false;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            // Shared cached tracks must outlive any single cell. See LoadScore above.
        }
    }
}
