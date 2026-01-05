using Newtonsoft.Json;

namespace SearchService.Models;

/// <summary>
/// Squad assignment information (nested object)
/// </summary>
public class SquadInfo
{
    [JsonProperty("squad_id")]
    public int SquadId { get; set; }

    [JsonProperty("team_id")]
    public int TeamId { get; set; }

    [JsonProperty("join_date")]
    public DateTime? JoinDate { get; set; }

    [JsonProperty("leave_date")]
    public DateTime? LeaveDate { get; set; }

    [JsonProperty("contract_value")]
    public decimal? ContractValue { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; }
}

/// <summary>
/// Player document for Elasticsearch indexing
/// Combines player data with squad information
/// </summary>
public class PlayerDocument
{
    [JsonProperty("player_id")]
    public int PlayerId { get; set; }

    [JsonProperty("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("last_name")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("full_name")]
    public string FullName => $"{FirstName} {LastName}";

    [JsonProperty("date_of_birth")]
    public DateTime? DateOfBirth { get; set; }

    [JsonProperty("age")]
    public int? Age => DateOfBirth.HasValue 
        ? (int)((DateTime.UtcNow - DateOfBirth.Value).TotalDays / 365.25) 
        : null;

    [JsonProperty("nationality")]
    public string? Nationality { get; set; }

    [JsonProperty("position")]
    public string? Position { get; set; }

    [JsonProperty("jersey_number")]
    public int? JerseyNumber { get; set; }

    [JsonProperty("height_cm")]
    public int? HeightCm { get; set; }

    [JsonProperty("weight_kg")]
    public int? WeightKg { get; set; }

    [JsonProperty("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// List of all squad assignments (historical and current)
    /// </summary>
    [JsonProperty("squads")]
    public List<SquadInfo> Squads { get; set; } = [];

    /// <summary>
    /// Current active team ID (derived from active squad)
    /// </summary>
    [JsonProperty("current_team_id")]
    public int? CurrentTeamId => Squads.FirstOrDefault(s => s.IsActive)?.TeamId;

    /// <summary>
    /// Whether player has any active squad assignment
    /// </summary>
    [JsonProperty("is_active")]
    public bool IsActive => Squads.Any(s => s.IsActive);

    /// <summary>
    /// All team IDs the player has been associated with
    /// </summary>
    [JsonProperty("team_ids")]
    public List<int> TeamIds => Squads.Select(s => s.TeamId).Distinct().ToList();

    [JsonProperty("indexed_at")]
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Search result wrapper
/// </summary>
public class SearchResult<T>
{
    public IReadOnlyCollection<T> Documents { get; set; } = [];
    public long TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// Player search request
/// </summary>
public class PlayerSearchRequest
{
    public string? Query { get; set; }
    public string? Position { get; set; }
    public string? Nationality { get; set; }
    public int? TeamId { get; set; }
    public bool? IsActive { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
