// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Rulesets.LazerTourney.Tournament.IO;
using osu.Game.Rulesets.LazerTourney.Tournament.Models;
using osu.Game.Rulesets.LazerTourney.Tournament.Online;
using osu.Game.Screens;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Users;

namespace osu.Game.Rulesets.LazerTourney.Tournament
{
    /// <summary>
    /// The entry-point screen for the tournament plugin. Wraps the tournament data (bracket,
    /// teams, rounds) and the online room state, and hosts <see cref="TournamentSceneManager"/>
    /// as a child <see cref="CompositeDrawable"/>.
    /// </summary>
    [Cached(typeof(TournamentGameBase))]
    public partial class TournamentGameBase : OsuScreen
    {
        public const string BRACKET_FILENAME = @"bracket.json";

        private LadderInfo ladder = new LadderInfo();
        private TournamentStorage storage = null!;
        private DependencyContainer dependencies = null!;
        private BeatmapLookupCache beatmapCache = null!;

        [Resolved]
        private RulesetStore rulesetStore { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> globalRuleset { get; set; } = null!;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        protected Task BracketLoadTask => bracketLoadTaskCompletionSource.Task;

        private readonly TaskCompletionSource<bool> bracketLoadTaskCompletionSource = new TaskCompletionSource<bool>();

        public TournamentOnlineState OnlineState = null!;

        /// <summary>
        /// Exit the tournament client. Called from the setup screen's exit button.
        /// </summary>
        public void ExitTournament()
        {
            this.Exit();
            // TODO: cursor, toolbar, etc.
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            return dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
        }

        /// <summary>
        /// Caches a dependency for the tournament subtree after initial load.
        /// </summary>
        protected void CacheDependency(object value) => dependencies.Cache(value);

        private TournamentSpriteText initialisationText = null!;

        public override bool AllowUserExit => false;

        [BackgroundDependencyLoader]
        private void load(Storage baseStorage, BeatmapLookupCache beatmapCache)
        {
            this.beatmapCache = beatmapCache;

            dependencies.CacheAs<Storage>(storage = new TournamentStorage(baseStorage));
            dependencies.CacheAs(storage);

            dependencies.Cache(new TournamentVideoResourceStore(storage));

            textures.AddTextureSource(new TextureLoaderStore(new StorageBackedResourceStore(storage)));

            OnlineState = new TournamentOnlineState();
            dependencies.Cache(OnlineState);
            // Must be added to the hierarchy, otherwise dependency injection never runs
            // and the subscribed room events never fire.
            AddInternal(OnlineState);

            var ongoingOperationTracker = new OngoingOperationTracker();
            dependencies.Cache(ongoingOperationTracker);
            AddInternal(ongoingOperationTracker);

            AddInternal(initialisationText = new TournamentSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Font = OsuFont.Torus.With(size: 32),
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Task.Run(readBracket);
        }

        private async Task readBracket()
        {
            try
            {
                if (storage.Exists(BRACKET_FILENAME))
                {
                    using (Stream stream = storage.GetStream(BRACKET_FILENAME, FileAccess.Read, FileMode.Open))
                    using (var sr = new StreamReader(stream))
                    {
                        ladder = JsonConvert.DeserializeObject<LadderInfo>(await sr.ReadToEndAsync().ConfigureAwait(false), new JsonPointConverter()) ?? ladder;
                    }
                }

                var resolvedRuleset = ladder.Ruleset.Value != null
                    ? rulesetStore.GetRuleset(ladder.Ruleset.Value.ShortName)
                    : rulesetStore.AvailableRulesets.First();

                ladder.Ruleset.Value = null;
                ladder.Ruleset.Value = resolvedRuleset;

                bool addedInfo = false;

                foreach (var match in ladder.Matches)
                {
                    match.Team1.Value = ladder.Teams.FirstOrDefault(t => t.Acronym.Value == match.Team1Acronym);
                    match.Team2.Value = ladder.Teams.FirstOrDefault(t => t.Acronym.Value == match.Team2Acronym);

                    foreach (var conditional in match.ConditionalMatches)
                    {
                        conditional.Team1.Value = ladder.Teams.FirstOrDefault(t => t.Acronym.Value == conditional.Team1Acronym);
                        conditional.Team2.Value = ladder.Teams.FirstOrDefault(t => t.Acronym.Value == conditional.Team2Acronym);
                        conditional.Round.Value = match.Round.Value;
                    }
                }

                foreach (var pair in ladder.Progressions)
                {
                    var src = ladder.Matches.FirstOrDefault(p => p.ID == pair.SourceID);
                    var dest = ladder.Matches.FirstOrDefault(p => p.ID == pair.TargetID);

                    if (src == null)
                        continue;

                    if (dest != null)
                    {
                        if (pair.Losers)
                            src.LosersProgression.Value = dest;
                        else
                            src.Progression.Value = dest;
                    }
                }

                foreach (var round in ladder.Rounds)
                {
                    foreach (int id in round.Matches)
                    {
                        var found = ladder.Matches.FirstOrDefault(p => p.ID == id);

                        if (found != null)
                        {
                            found.Round.Value = round;
                            if (round.StartDate.Value > found.Date.Value)
                                found.Date.Value = round.StartDate.Value;
                        }
                    }
                }

                addedInfo |= addPlayers();
                addedInfo |= await addRoundBeatmaps().ConfigureAwait(false);
                addedInfo |= await addSeedingBeatmaps().ConfigureAwait(false);

                if (addedInfo)
                    saveChanges();

                ladder.CurrentMatch.Value = ladder.Matches.FirstOrDefault(p => p.Current.Value);

                ladder.Ruleset.BindValueChanged(r =>
                {
                    foreach (var team in ladder.Teams)
                    {
                        foreach (var player in team.Players)
                            player.Rank = null;
                    }

                    SaveChanges();
                });
            }
            catch (Exception e)
            {
                bracketLoadTaskCompletionSource.SetException(e);
                return;
            }

            Schedule(() =>
            {
                globalRuleset.BindTo(ladder.Ruleset);

                dependencies.Cache(ladder);

                bracketLoadTaskCompletionSource.SetResult(true);

                initialisationText.Expire();
            });
        }

        private bool addPlayers()
        {
            var playersRequiringPopulation = ladder.Teams
                                                   .SelectMany(t => t.Players)
                                                   .Where(p => string.IsNullOrEmpty(p.Username)
                                                               || p.CountryCode == CountryCode.Unknown
                                                               || p.Rank == null).ToList();

            if (playersRequiringPopulation.Count == 0)
                return false;

            for (int i = 0; i < playersRequiringPopulation.Count; i++)
            {
                var p = playersRequiringPopulation[i];
                PopulatePlayer(p, immediate: true);
                updateLoadProgressMessage($"Populating user stats ({i} / {playersRequiringPopulation.Count})");
            }

            return true;
        }

        private async Task<bool> addRoundBeatmaps()
        {
            bool normalised = normaliseRoundBeatmapMods();

            var beatmapsRequiringPopulation = ladder.Rounds
                                                     .SelectMany(r => r.Beatmaps)
                                                     .Where(b => (b.Beatmap == null || b.Beatmap?.OnlineID == 0) && b.ID > 0).ToList();

            if (beatmapsRequiringPopulation.Count == 0)
                return normalised;

            for (int i = 0; i < beatmapsRequiringPopulation.Count; i++)
            {
                var b = beatmapsRequiringPopulation[i];

                var populated = await beatmapCache.GetBeatmapAsync(b.ID).ConfigureAwait(false);
                if (populated != null)
                    b.Beatmap = new TournamentBeatmap(populated);

                updateLoadProgressMessage($"Populating round beatmaps ({i} / {beatmapsRequiringPopulation.Count})");
            }

            return true;
        }

        /// <summary>
        /// Fills gameplay-mod defaults for round beatmaps loaded from old brackets that lack the new fields.
        /// Old entries get <c>freestyle=false</c>, no required mods and all free mods.
        /// Entries that explicitly store empty arrays are left untouched.
        /// </summary>
        private bool normaliseRoundBeatmapMods()
        {
            var rulesetInfo = ladder.Ruleset.Value ?? rulesetStore.AvailableRulesets.FirstOrDefault();

            if (rulesetInfo == null)
                return false;

            Ruleset? ruleset = rulesetInfo.CreateInstance();

            // Same validity gate as lazer room creation (excludes system/non-playable/unimplemented mods).
            APIMod[] allFreeMods = ruleset == null
                ? Array.Empty<APIMod>()
                : RoundBeatmap.GetAllFreeMods(ruleset);

            bool changed = false;

            foreach (var b in ladder.Rounds.SelectMany(r => r.Beatmaps))
            {
                if (b.RequiredMods == null)
                {
                    b.RequiredMods = Array.Empty<APIMod>();
                    changed = true;
                }
                else if (b.Freestyle && ruleset != null)
                {
                    // Strip required mods that may not stay required in freestyle (same as the editor toggle).
                    APIMod[] filtered = RoundBeatmap.FilterFreestyleRequiredMods(b.RequiredMods, ruleset);

                    if (!filtered.Select(m => m.Acronym).SequenceEqual(b.RequiredMods.Select(m => m.Acronym)))
                    {
                        b.RequiredMods = filtered;
                        changed = true;
                    }
                }

                if (b.AllowedMods == null)
                {
                    b.AllowedMods = b.Freestyle ? Array.Empty<APIMod>() : allFreeMods;
                    changed = true;
                }
            }

            return changed;
        }

        private async Task<bool> addSeedingBeatmaps()
        {
            var beatmapsRequiringPopulation = ladder.Teams
                                                    .SelectMany(r => r.SeedingResults)
                                                    .SelectMany(r => r.Beatmaps)
                                                    .Where(b => (b.Beatmap == null || b.Beatmap.OnlineID == 0) && b.ID > 0).ToList();

            if (beatmapsRequiringPopulation.Count == 0)
                return false;

            for (int i = 0; i < beatmapsRequiringPopulation.Count; i++)
            {
                var b = beatmapsRequiringPopulation[i];

                var populated = await beatmapCache.GetBeatmapAsync(b.ID).ConfigureAwait(false);
                if (populated != null)
                    b.Beatmap = new TournamentBeatmap(populated);

                updateLoadProgressMessage($"Populating seeding beatmaps ({i} / {beatmapsRequiringPopulation.Count})");
            }

            return true;
        }

        private void updateLoadProgressMessage(string s) => Schedule(() => initialisationText.Text = s);

        public void PopulatePlayer(TournamentUser user, Action? success = null, Action? failure = null, bool immediate = false)
        {
            var req = new GetUserRequest(user.OnlineID, ladder.Ruleset.Value);

            if (immediate)
            {
                api.Perform(req);
                populate();
            }
            else
            {
                req.Success += _ => { populate(); };
                req.Failure += _ =>
                {
                    user.OnlineID = 1;
                    failure?.Invoke();
                };

                api.Queue(req);
            }

            void populate()
            {
                var res = req.Response;

                if (res == null)
                    return;

                user.OnlineID = res.Id;

                user.Username = res.Username;
                user.CoverUrl = res.CoverUrl;
                user.CountryCode = res.CountryCode;
                user.Rank = res.Statistics?.GlobalRank;

                success?.Invoke();
            }
        }

        public void SaveChanges()
        {
            if (!bracketLoadTaskCompletionSource.Task.IsCompletedSuccessfully)
            {
                Logger.Log("Inhibiting bracket save as bracket parsing failed");
                return;
            }

            saveChanges();
        }

        private void saveChanges()
        {
            string serialisedLadder = GetSerialisedLadder();

            using (var stream = storage.CreateFileSafely(BRACKET_FILENAME))
            using (var sw = new StreamWriter(stream))
                sw.Write(serialisedLadder);
        }

        public string GetSerialisedLadder()
        {
            foreach (var r in ladder.Rounds)
                r.Matches = ladder.Matches.Where(p => p.Round.Value == r).Select(p => p.ID).ToList();

            ladder.Progressions = ladder.Matches.Where(p => p.Progression.Value != null).Select(p => new TournamentProgression(p.ID, p.Progression.Value.AsNonNull().ID)).Concat(
                                            ladder.Matches.Where(p => p.LosersProgression.Value != null).Select(p => new TournamentProgression(p.ID, p.LosersProgression.Value.AsNonNull().ID, true)))
                                        .ToList();

            return JsonConvert.SerializeObject(ladder,
                new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Ignore,
                    Converters = new JsonConverter[] { new JsonPointConverter() }
                });
        }
    }
}
