using Microsoft.AspNetCore.Mvc;
using SearchService.Models;
using SearchService.Services;

namespace SearchService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayersController : ControllerBase
{
    private readonly IElasticsearchService _elasticsearchService;
    private readonly ILogger<PlayersController> _logger;

    public PlayersController(IElasticsearchService elasticsearchService, ILogger<PlayersController> logger)
    {
        _elasticsearchService = elasticsearchService;
        _logger = logger;
    }

    /// <summary>
    /// Search for players with various filters
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<SearchResult<PlayerDocument>>> Search([FromQuery] PlayerSearchRequest request)
    {
        _logger.LogInformation("Searching players with query: {Query}", request.Query);
        var result = await _elasticsearchService.SearchPlayersAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Get a player by ID
    /// </summary>
    [HttpGet("{playerId:int}")]
    public async Task<ActionResult<PlayerDocument>> GetPlayer(int playerId)
    {
        var player = await _elasticsearchService.GetPlayerAsync(playerId);
        
        if (player == null)
            return NotFound(new { message = $"Player with ID {playerId} not found" });
        
        return Ok(player);
    }

    /// <summary>
    /// Get players by position
    /// </summary>
    [HttpGet("position/{position}")]
    public async Task<ActionResult<SearchResult<PlayerDocument>>> GetByPosition(string position, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var request = new PlayerSearchRequest
        {
            Position = position,
            Page = page,
            PageSize = pageSize
        };
        
        var result = await _elasticsearchService.SearchPlayersAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Get players by nationality
    /// </summary>
    [HttpGet("nationality/{nationality}")]
    public async Task<ActionResult<SearchResult<PlayerDocument>>> GetByNationality(string nationality, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var request = new PlayerSearchRequest
        {
            Nationality = nationality,
            Page = page,
            PageSize = pageSize
        };
        
        var result = await _elasticsearchService.SearchPlayersAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Get players by team
    /// </summary>
    [HttpGet("team/{teamId:int}")]
    public async Task<ActionResult<SearchResult<PlayerDocument>>> GetByTeam(int teamId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var request = new PlayerSearchRequest
        {
            TeamId = teamId,
            Page = page,
            PageSize = pageSize
        };
        
        var result = await _elasticsearchService.SearchPlayersAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Get active players
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<SearchResult<PlayerDocument>>> GetActivePlayers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var request = new PlayerSearchRequest
        {
            IsActive = true,
            Page = page,
            PageSize = pageSize
        };
        
        var result = await _elasticsearchService.SearchPlayersAsync(request);
        return Ok(result);
    }
}

