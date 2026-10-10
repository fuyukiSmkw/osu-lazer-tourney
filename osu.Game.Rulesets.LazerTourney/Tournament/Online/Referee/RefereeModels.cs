// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;

namespace osu.Game.Rulesets.LazerTourney.Tournament.Online.Referee
{
    /// <summary>
    /// Request body for the referee hub <c>MakeRoom</c> method.
    /// Mirrors <c>osu.Server.Spectator.Hubs.Referee.Models.Requests.MakeRoomRequest</c>.
    /// Property names must stay snake_case to match the server JSON schema.
    /// </summary>
    public class MakeRoomRequest
    {
        [JsonPropertyName("ruleset_id")]
        public int RulesetId { get; set; }

        [JsonPropertyName("beatmap_id")]
        public int BeatmapId { get; set; }

        [JsonPropertyName("name")]
        public string RoomName { get; set; } = string.Empty;

        /// <summary>
        /// 0 means unlimited participants (no slots); otherwise 2-16.
        /// </summary>
        [JsonPropertyName("max_participants")]
        public byte MaxParticipants { get; set; }
    }

    /// <summary>
    /// Request body for the referee hub <c>ChangeRoomSettings</c> method.
    /// Mirrors <c>osu.Server.Spectator.Hubs.Referee.Models.Requests.ChangeRoomSettingsRequest</c>.
    /// Null properties are omitted from the payload, which the server treats as "keep the previous value".
    /// </summary>
    public class ChangeRoomSettingsRequest
    {
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }

        /// <summary>
        /// Empty string clears the password.
        /// </summary>
        [JsonPropertyName("password")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Password { get; set; }

        /// <summary>
        /// Either "head_to_head" or "team_versus".
        /// </summary>
        [JsonPropertyName("type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MatchType { get; set; }

        /// <summary>
        /// Either "host_only", "all_players" or "all_players_round_robin".
        /// </summary>
        [JsonPropertyName("queue_mode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? QueueMode { get; set; }

        /// <summary>
        /// 0 removes the limit; otherwise 2-16.
        /// </summary>
        [JsonPropertyName("max_participants")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public byte? MaxParticipants { get; set; }
    }

    /// <summary>
    /// Response returned by the referee hub <c>MakeRoom</c> and <c>JoinRoom</c> methods.
    /// Mirrors <c>osu.Server.Spectator.Hubs.Referee.Models.Responses.RoomJoinedResponse</c>.
    /// Only the fields needed by the tournament client are declared; unknown fields are ignored on deserialisation.
    /// </summary>
    public class RoomJoinedResponse
    {
        [JsonPropertyName("room_id")]
        public long RoomId { get; set; }

        [JsonPropertyName("chat_channel_id")]
        public int ChatChannelId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("max_participants")]
        public byte MaxParticipants { get; set; }

        [JsonPropertyName("queue_mode")]
        public string QueueMode { get; set; } = string.Empty;
    }
}
