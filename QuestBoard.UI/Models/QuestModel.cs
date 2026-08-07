using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QuestBoard.UI.Models
{
    public class QuestHistoryEntry
    {
        [JsonPropertyName("at")]
        public string At { get; set; } = string.Empty;

        [JsonPropertyName("actor")]
        public string Actor { get; set; } = string.Empty;

        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("note")]
        public string Note { get; set; } = string.Empty;

        public string FormattedTime
        {
            get
            {
                if (DateTime.TryParse(At, out var dt))
                    return dt.ToLocalTime().ToString("g");
                return At;
            }
        }
    }

    public class QuestModel
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "ready";

        [JsonPropertyName("priority")]
        public string Priority { get; set; } = "medium";

        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        [JsonPropertyName("claim_expires_at")]
        public string? ClaimExpiresAt { get; set; }

        [JsonPropertyName("next_action")]
        public string NextAction { get; set; } = string.Empty;

        [JsonPropertyName("context")]
        public string Context { get; set; } = string.Empty;

        [JsonPropertyName("blocker")]
        public string? Blocker { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; } = string.Empty;

        [JsonPropertyName("history")]
        public List<QuestHistoryEntry> History { get; set; } = new();

        public string FilePath { get; set; } = string.Empty;

        public bool IsClaimExpired
        {
            get
            {
                if (string.IsNullOrEmpty(ClaimExpiresAt)) return false;
                if (DateTime.TryParse(ClaimExpiresAt, out var dt))
                    return DateTime.UtcNow > dt;
                return false;
            }
        }

        public string StatusBadgeText => Status.ToUpperInvariant();

        public string PriorityBadgeText => Priority.ToUpperInvariant();
    }
}
