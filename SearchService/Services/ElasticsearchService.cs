using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using SearchService.Models;

namespace SearchService.Services;

public class ElasticsearchService : IElasticsearchService
{
    private readonly ElasticsearchClient _client;
    private readonly string _indexName;
    private readonly ILogger<ElasticsearchService> _logger;

    public ElasticsearchService(IConfiguration configuration, ILogger<ElasticsearchService> logger)
    {
        _logger = logger;
        _indexName = configuration["Elasticsearch:IndexName"] ?? "players";
        
        var elasticsearchUrl = configuration["Elasticsearch:Url"] ?? "http://localhost:9200";
        var settings = new ElasticsearchClientSettings(new Uri(elasticsearchUrl))
            .DefaultIndex(_indexName)
            .EnableDebugMode();
        
        _client = new ElasticsearchClient(settings);
    }

    public async Task<bool> CreateIndexAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var existsResponse = await _client.Indices.ExistsAsync(_indexName, cancellationToken);
            
            if (existsResponse.IsValidResponse)
            {
                _logger.LogInformation("Index {IndexName} already exists", _indexName);
                return true;
            }

            var createResponse = await _client.Indices.CreateAsync(_indexName, c => c
                .Mappings(m => m
                    .Properties<PlayerDocument>(p => p
                        .IntegerNumber(i => i.PlayerId)
                        .Text(t => t.FirstName, t => t.Analyzer("standard"))
                        .Text(t => t.LastName, t => t.Analyzer("standard"))
                        .Text(t => t.FullName, t => t.Analyzer("standard").Fields(f => f.Keyword("keyword")))
                        .Date(d => d.DateOfBirth)
                        .Keyword(k => k.Nationality)
                        .Keyword(k => k.Position)
                        .IntegerNumber(i => i.JerseyNumber)
                        .IntegerNumber(i => i.HeightCm)
                        .IntegerNumber(i => i.WeightKg)
                        .IntegerNumber(i => i.CurrentTeamId)
                        .Boolean(b => b.IsActive)
                        .IntegerNumber(i => i.TeamIds)
                        .Nested("squads", n => n
                            .Properties(sp => sp
                                .IntegerNumber("squad_id")
                                .IntegerNumber("team_id")
                                .Date("join_date")
                                .Date("leave_date")
                                .FloatNumber("contract_value")
                                .Boolean("is_active")
                            )
                        )
                        .Date(d => d.IndexedAt)
                    )
                ), cancellationToken);

            if (createResponse.IsValidResponse)
            {
                _logger.LogInformation("Successfully created index {IndexName}", _indexName);
                return true;
            }

            _logger.LogError("Failed to create index {IndexName}: {Error}", _indexName, createResponse.DebugInformation);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating index {IndexName}", _indexName);
            return false;
        }
    }

    public async Task<bool> IndexPlayerAsync(PlayerDocument player, CancellationToken cancellationToken = default)
    {
        try
        {
            player.IndexedAt = DateTime.UtcNow;
            
            var request = new IndexRequest<PlayerDocument>(_indexName, player.PlayerId.ToString())
            {
                Document = player
            };
            var response = await _client.IndexAsync(request, cancellationToken);

            if (response.IsValidResponse)
            {
                _logger.LogInformation("Indexed player {PlayerId}: {FullName} with {SquadCount} squad(s)", 
                    player.PlayerId, player.FullName, player.Squads.Count);
                return true;
            }

            _logger.LogError("Failed to index player {PlayerId}: {Error}", player.PlayerId, response.DebugInformation);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing player {PlayerId}", player.PlayerId);
            return false;
        }
    }

    public async Task<bool> DeletePlayerAsync(int playerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.DeleteAsync(_indexName, playerId.ToString(), cancellationToken);

            if (response.IsValidResponse)
            {
                _logger.LogInformation("Deleted player {PlayerId} from index", playerId);
                return true;
            }

            _logger.LogWarning("Failed to delete player {PlayerId}: {Error}", playerId, response.DebugInformation);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting player {PlayerId}", playerId);
            return false;
        }
    }

    public async Task<PlayerDocument?> GetPlayerAsync(int playerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetAsync<PlayerDocument>(_indexName, playerId.ToString(), cancellationToken);

            if (response.IsValidResponse && response.Found)
            {
                return response.Source;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting player {PlayerId}", playerId);
            return null;
        }
    }

    public async Task<SearchResult<PlayerDocument>> SearchPlayersAsync(PlayerSearchRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var mustQueries = new List<Query>();

            // Full-text search on name fields
            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                mustQueries.Add(new MultiMatchQuery
                {
                    Query = request.Query,
                    Fields = new[] { "firstName", "lastName", "fullName" },
                    Fuzziness = new Fuzziness("AUTO")
                });
            }

            // Filter by position
            if (!string.IsNullOrWhiteSpace(request.Position))
            {
                mustQueries.Add(new TermQuery("position") { Value = request.Position });
            }

            // Filter by nationality
            if (!string.IsNullOrWhiteSpace(request.Nationality))
            {
                mustQueries.Add(new TermQuery("nationality") { Value = request.Nationality });
            }

            // Filter by team (searches in team_ids array for any team association)
            if (request.TeamId.HasValue)
            {
                mustQueries.Add(new NestedQuery
                {
                    Path = "squads",
                    Query = new BoolQuery
                    {
                        Must = [new TermQuery("squads.teamId") { Value = request.TeamId.Value }]
                    }
                });
            }

            // Filter by active status
            if (request.IsActive.HasValue)
            {
                mustQueries.Add(new TermQuery("isActive") { Value = request.IsActive.Value });
            }

            // Age range filter (calculate based on date of birth)
            if (request.MinAge.HasValue || request.MaxAge.HasValue)
            {
                var now = DateTime.UtcNow;
                DateMath? minDate = request.MaxAge.HasValue ? DateMath.Anchored(now.AddYears(-request.MaxAge.Value - 1)) : null;
                DateMath? maxDate = request.MinAge.HasValue ? DateMath.Anchored(now.AddYears(-request.MinAge.Value)) : null;

                var dateRangeQuery = new DateRangeQuery(new Field("dateOfBirth"))
                {
                    Gte = minDate,
                    Lte = maxDate
                };
                mustQueries.Add(Query.Range(new RangeQuery(dateRangeQuery)));
            }

            var from = (request.Page - 1) * request.PageSize;

            var searchResponse = await _client.SearchAsync<PlayerDocument>(s => s
                .Index(_indexName)
                .From(from)
                .Size(request.PageSize)
                .Query(q => q
                    .Bool(b => b
                        .Must(mustQueries.ToArray())
                    )
                )
                .Sort(so => so.Field("fullName.keyword", new FieldSort { Order = SortOrder.Asc, UnmappedType = FieldType.Keyword })),
                cancellationToken);

            if (searchResponse.IsValidResponse)
            {
                return new SearchResult<PlayerDocument>
                {
                    Documents = searchResponse.Documents,
                    TotalCount = searchResponse.Total,
                    Page = request.Page,
                    PageSize = request.PageSize
                };
            }

            _logger.LogError("Search failed: {Error}", searchResponse.DebugInformation);
            return new SearchResult<PlayerDocument>
            {
                Documents = [],
                TotalCount = 0,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching players");
            return new SearchResult<PlayerDocument>
            {
                Documents = [],
                TotalCount = 0,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
    }

    public async Task<bool> UpsertPlayerSquadAsync(int playerId, SquadInfo squadInfo, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get existing player
            var existingPlayer = await GetPlayerAsync(playerId, cancellationToken);
            
            if (existingPlayer == null)
            {
                // Player doesn't exist yet - create a placeholder with squad info
                // The player details will be filled in when the player event arrives
                _logger.LogInformation("Player {PlayerId} not found. Creating placeholder with squad {SquadId} (team {TeamId})", 
                    playerId, squadInfo.SquadId, squadInfo.TeamId);
                
                existingPlayer = new PlayerDocument
                {
                    PlayerId = playerId,
                    FirstName = string.Empty,
                    LastName = string.Empty,
                    Squads = [squadInfo]
                };
                
                return await IndexPlayerAsync(existingPlayer, cancellationToken);
            }

            // Find existing squad entry or add new one
            var existingSquad = existingPlayer.Squads.FirstOrDefault(s => s.SquadId == squadInfo.SquadId);
            
            if (existingSquad != null)
            {
                // Update existing squad
                existingSquad.TeamId = squadInfo.TeamId;
                existingSquad.JoinDate = squadInfo.JoinDate;
                existingSquad.LeaveDate = squadInfo.LeaveDate;
                existingSquad.ContractValue = squadInfo.ContractValue;
                existingSquad.IsActive = squadInfo.IsActive;
                
                _logger.LogInformation("Updated squad {SquadId} for player {PlayerId}", squadInfo.SquadId, playerId);
            }
            else
            {
                // Add new squad
                existingPlayer.Squads.Add(squadInfo);
                _logger.LogInformation("Added squad {SquadId} (team {TeamId}) for player {PlayerId}", 
                    squadInfo.SquadId, squadInfo.TeamId, playerId);
            }

            existingPlayer.IndexedAt = DateTime.UtcNow;
            return await IndexPlayerAsync(existingPlayer, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting squad {SquadId} for player {PlayerId}", squadInfo.SquadId, playerId);
            return false;
        }
    }

    public async Task<bool> RemovePlayerSquadAsync(int playerId, int squadId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get existing player
            var existingPlayer = await GetPlayerAsync(playerId, cancellationToken);
            
            if (existingPlayer == null)
            {
                _logger.LogWarning("Cannot remove squad from non-existent player {PlayerId}", playerId);
                return false;
            }

            // Remove squad entry
            var removed = existingPlayer.Squads.RemoveAll(s => s.SquadId == squadId);
            
            if (removed > 0)
            {
                _logger.LogInformation("Removed squad {SquadId} from player {PlayerId}", squadId, playerId);
                existingPlayer.IndexedAt = DateTime.UtcNow;
                return await IndexPlayerAsync(existingPlayer, cancellationToken);
            }
            
            _logger.LogWarning("Squad {SquadId} not found for player {PlayerId}", squadId, playerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing squad {SquadId} from player {PlayerId}", squadId, playerId);
            return false;
        }
    }
}
