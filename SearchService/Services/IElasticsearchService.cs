using SearchService.Models;

namespace SearchService.Services;

public interface IElasticsearchService
{
    Task<bool> CreateIndexAsync(CancellationToken cancellationToken = default);
    Task<bool> IndexPlayerAsync(PlayerDocument player, CancellationToken cancellationToken = default);
    Task<bool> DeletePlayerAsync(int playerId, CancellationToken cancellationToken = default);
    Task<PlayerDocument?> GetPlayerAsync(int playerId, CancellationToken cancellationToken = default);
    Task<SearchResult<PlayerDocument>> SearchPlayersAsync(PlayerSearchRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add or update a squad assignment for a player
    /// </summary>
    Task<bool> UpsertPlayerSquadAsync(int playerId, SquadInfo squadInfo, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Remove a squad assignment from a player
    /// </summary>
    Task<bool> RemovePlayerSquadAsync(int playerId, int squadId, CancellationToken cancellationToken = default);
}
