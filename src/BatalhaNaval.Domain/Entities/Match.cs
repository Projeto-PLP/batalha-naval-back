using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using BatalhaNaval.Domain.Enums;
using BatalhaNaval.Domain.ValueObjects;

namespace BatalhaNaval.Domain.Entities;

public class Match
{
    private bool _player1Ready;
    private bool _player2Ready;

    // ====================================================================
    // CONSTRUTOR
    // ====================================================================

    public Match(Guid player1Id, GameMode mode, Difficulty? aiDifficulty = null, Guid? player2Id = null)
    {
        Id = Guid.NewGuid();
        Player1Id = player1Id;
        Player2Id = player2Id;
        Mode = mode;
        AiDifficulty = aiDifficulty;
        Status = MatchStatus.Setup;

        Player1Board = new Board();
        Player2Board = new Board();

        CurrentTurnPlayerId = player1Id;
    }

    // ====================================================================
    // ESTATÍSTICAS E CONTROLE DE ESTADO
    // ====================================================================

    [Column("player1_hits")] public int Player1Hits { get; private set; }

    [Column("player2_hits")] public int Player2Hits { get; private set; }

    [Column("player1_consecutive_hits")] public int Player1ConsecutiveHits { get; private set; }

    [Column("player2_consecutive_hits")] public int Player2ConsecutiveHits { get; private set; }

    [Column("has_moved_this_turn")] public bool HasMovedThisTurn { get; private set; }

    // ====================================================================
    // PROPRIEDADES PRINCIPAIS
    // ====================================================================

    [Description("Identificador único da partida")]
    public Guid Id { get; private set; }

    [Description("Identificador único do jogador 1")]
    public Guid Player1Id { get; }

    [Description("Identificador único do jogador 2")]
    public Guid? Player2Id { get; }

    [Description("Tabuleiro do jogador 1")]
    public Board Player1Board { get; private set; }

    [Description("Tabuleiro do jogador 2")]
    public Board Player2Board { get; private set; }

    [Description("Modo de jogo")] public GameMode Mode { get; }

    [Description("Dificuldade da IA, se aplicável")]
    public Difficulty? AiDifficulty { get; }

    [Description("Status atual da partida")]
    [Column("status")]
    public MatchStatus Status { get; set; }

    [Description("Indica se a partida está finalizada")]
    [Column("is_finished")]
    public bool IsFinished => Status == MatchStatus.Finished;

    [Description("Hora de encerramento da partida")]
    [Column("finished_at")]
    public DateTime? FinishedAt { get; set; }

    [Description("Identificador único do jogador atual")]
    public Guid CurrentTurnPlayerId { get; private set; }

    [Description("Identificador único do vencedor")]
    public Guid? WinnerId { get; set; }

    [Description("Data e hora de início da partida")]
    public DateTime StartedAt { get; private set; }

    [Description("Data e hora do último movimento")]
    public DateTime LastMoveAt { get; private set; }

    // ====================================================================
    // MÉTODOS DE SUPORTE AO REDIS (MAPPING)
    // ====================================================================

    public MatchRedis ToRedisDto()
    {
        return new MatchRedis
        {
            MatchId = Id.ToString(),
            Player1Id = Player1Id.ToString(),
            Player2Id = Player2Id?.ToString(),
            GameMode = MapGameModeToRedis(Mode),
            AiDifficulty = AiDifficulty.HasValue ? MapDifficultyToRedis(AiDifficulty.Value) : null,
            Status = MapStatusToRedis(Status),

            TurnPlayerId = CurrentTurnPlayerId.ToString(),
            TurnStartedAt = new DateTimeOffset(LastMoveAt).ToUnixTimeSeconds(),

            // Mapeia Stats
            P1_Stats = new PlayerStatsRedis
            {
                Hits = Player1Hits,
                Streak = Player1ConsecutiveHits
            },
            P2_Stats = new PlayerStatsRedis
            {
                Hits = Player2Hits,
                Streak = Player2ConsecutiveHits
            },

            // Mapeia Tabuleiros
            Boards = new MatchBoardsRedis
            {
                P1 = MapBoardToRedis(Player1Board),
                P2 = MapBoardToRedis(Player2Board)
            }
        };
    }

    public static Match FromRedisDto(MatchRedis dto)
    {
        // 1. Converte Enums e IDs de volta
        var p1Id = Guid.Parse(dto.Player1Id);
        var p2Id = string.IsNullOrEmpty(dto.Player2Id) ? (Guid?)null : Guid.Parse(dto.Player2Id);
        var mode = MapGameModeFromRedis(dto.GameMode);
        var difficulty = dto.AiDifficulty.HasValue ? MapDifficultyFromRedis(dto.AiDifficulty.Value) : (Difficulty?)null;

        // 2. Cria instância
        var match = new Match(p1Id, mode, difficulty, p2Id);

        // 3. Hidrata propriedades
        match.Id = Guid.Parse(dto.MatchId);
        match.Status = MapStatusFromRedis(dto.Status);
        match.CurrentTurnPlayerId = string.IsNullOrEmpty(dto.TurnPlayerId) ? Guid.Empty : Guid.Parse(dto.TurnPlayerId);
        match.LastMoveAt = DateTimeOffset.FromUnixTimeSeconds(dto.TurnStartedAt).UtcDateTime;

        // Stats
        match.Player1Hits = dto.P1_Stats.Hits;
        match.Player1ConsecutiveHits = dto.P1_Stats.Streak;
        match.Player2Hits = dto.P2_Stats.Hits;
        match.Player2ConsecutiveHits = dto.P2_Stats.Streak;

        // Tabuleiros
        match.Player1Board = MapBoardFromRedis(dto.Boards.P1);
        match.Player2Board = MapBoardFromRedis(dto.Boards.P2);

        return match;
    }

    // --- Helpers de Mapeamento (Privados) ---

    private PlayerBoardRedis MapBoardToRedis(Board board)
    {
        var redisBoard = new PlayerBoardRedis
        {
            AliveShips = board.Ships.Count(s => !s.IsSunk),
            OceanGrid = new Dictionary<string, int>()
        };

        for (var x = 0; x < Board.Size; x++)
        for (var y = 0; y < Board.Size; y++)
        {
            var cell = board.Cells[x][y];
            // Mapeamos apenas o que não é água
            if (cell == CellState.Hit) redisBoard.OceanGrid[$"{x},{y}"] = 1;
            else if (cell == CellState.Missed) redisBoard.OceanGrid[$"{x},{y}"] = 0;
        }

        // Mapeia Navios
        redisBoard.Ships = board.Ships.Select(s => new ShipRedis
        {
            Id = s.Id.ToString(),
            Type = s.Name,
            Size = s.Size,
            Sunk = s.IsSunk,
            IsDamaged = s.HasBeenHit,
            Orientation = s.Orientation == ShipOrientation.Horizontal
                ? ShipOrientationRedis.HORIZONTAL
                : ShipOrientationRedis.VERTICAL,
            Segments = s.Coordinates.Select(c => new ShipSegmentRedis
            {
                X = c.X,
                Y = c.Y,
                Hit = c.IsHit
            }).ToList()
        }).ToList();

        return redisBoard;
    }

    private static Board MapBoardFromRedis(PlayerBoardRedis dto)
    {
        var board = new Board();

        // 1. Reconstrói os Navios (No tabuleiro limpo), Isso evita que o AddShip falhe ao encontrar uma célula já marcada como Hit
        foreach (var shipDto in dto.Ships)
        {
            var coords = shipDto.Segments.Select(s => new Coordinate(s.X, s.Y) { IsHit = s.Hit }).ToList();
            var orientation = shipDto.Orientation == ShipOrientationRedis.HORIZONTAL
                ? ShipOrientation.Horizontal
                : ShipOrientation.Vertical;

            var shipId = Guid.Parse(shipDto.Id);

            var ship = new Ship(shipId, shipDto.Type, shipDto.Size, coords, orientation);
            board.AddShip(ship);
        }

        // 2. Aplica o estado do Grid (Hits e Misses) por cima
        foreach (var kvp in dto.OceanGrid)
        {
            var coords = kvp.Key.Split(',');
            var x = int.Parse(coords[0]);
            var y = int.Parse(coords[1]);
            // 1 = Hit, 0 = Missed
            // Aqui sobrescrevemos o estado da célula, o que é permitido
            board.Cells[x][y] = kvp.Value == 1 ? CellState.Hit : CellState.Missed;
        }

        return board;
    }

    // Mappers de Enum
    private static GameModeRedis MapGameModeToRedis(GameMode mode)
    {
        return mode == GameMode.Classic ? GameModeRedis.CLASSIC : GameModeRedis.DYNAMIC;
    }

    private static GameMode MapGameModeFromRedis(GameModeRedis mode)
    {
        return mode == GameModeRedis.CLASSIC ? GameMode.Classic : GameMode.Dynamic;
    }

    private static MatchStatusRedis MapStatusToRedis(MatchStatus status)
    {
        return status switch
        {
            MatchStatus.Setup => MatchStatusRedis.SETUP,
            MatchStatus.InProgress => MatchStatusRedis.IN_PROGRESS,
            MatchStatus.Finished => MatchStatusRedis.FINISHED,
            _ => MatchStatusRedis.SETUP
        };
    }

    private static MatchStatus MapStatusFromRedis(MatchStatusRedis status)
    {
        return status switch
        {
            MatchStatusRedis.SETUP => MatchStatus.Setup,
            MatchStatusRedis.IN_PROGRESS => MatchStatus.InProgress,
            MatchStatusRedis.FINISHED => MatchStatus.Finished,
            _ => MatchStatus.Setup
        };
    }

    private static AiDifficultyRedis MapDifficultyToRedis(Difficulty diff)
    {
        return diff switch
        {
            Difficulty.Basic => AiDifficultyRedis.BASIC,
            Difficulty.Intermediate => AiDifficultyRedis.INTERMEDIATE,
            Difficulty.Advanced => AiDifficultyRedis.ADVANCED,
            _ => AiDifficultyRedis.BASIC
        };
    }

    private static Difficulty MapDifficultyFromRedis(AiDifficultyRedis diff)
    {
        return diff switch
        {
            AiDifficultyRedis.BASIC => Difficulty.Basic,
            AiDifficultyRedis.INTERMEDIATE => Difficulty.Intermediate,
            AiDifficultyRedis.ADVANCED => Difficulty.Advanced,
            _ => Difficulty.Basic
        };
    }

    // ====================================================================
    // LÓGICA DE NEGÓCIO
    // ====================================================================

    public void SetPlayerReady(Guid playerId)
    {
        if (Status != MatchStatus.Setup) return;

        if (playerId == Player1Id) _player1Ready = true;
        else if (playerId == Player2Id) _player2Ready = true;

        var isAiGame = Player2Id == null;
        if (_player1Ready && (_player2Ready || isAiGame)) StartGame();
    }

    private void StartGame()
    {
        Status = MatchStatus.InProgress;
        StartedAt = DateTime.UtcNow;
        LastMoveAt = DateTime.UtcNow;

        var random = new Random();
        var starter = random.Next(2);

        if (starter == 0)
            CurrentTurnPlayerId = Player1Id;
        else
            CurrentTurnPlayerId = Player2Id ?? Guid.Empty;

        HasMovedThisTurn = false;
    }

    public bool ExecuteShot(Guid playerId, int x, int y)
    {
        ValidateTurn(playerId);

        var targetBoard = playerId == Player1Id ? Player2Board : Player1Board;

        var isHit = targetBoard.ReceiveShot(x, y);

        if (isHit)
        {
            if (playerId == Player1Id)
            {
                Player1Hits++;
                Player1ConsecutiveHits++;
            }
            else
            {
                Player2Hits++;
                Player2ConsecutiveHits++;
            }
        }
        else
        {
            if (playerId == Player1Id) Player1ConsecutiveHits = 0;
            else Player2ConsecutiveHits = 0;
        }

        if (targetBoard.AllShipsSunk())
            FinishGame(playerId);
        else if (!isHit)
            SwitchTurn();
        else
            HasMovedThisTurn = false;

        LastMoveAt = DateTime.UtcNow;
        return isHit;
    }

    public void ExecuteShipMovement(Guid playerId, Guid shipId, MoveDirection direction)
    {
        if (Mode != GameMode.Dynamic)
            throw new InvalidOperationException("Movimentação de navios só é permitida no modo Dinâmico.");

        ValidateTurn(playerId);

        if (HasMovedThisTurn)
            throw new InvalidOperationException("Você já realizou um movimento neste turno. Agora deve atirar.");

        var myBoard = playerId == Player1Id ? Player1Board : Player2Board;

        myBoard.MoveShip(shipId, direction);

        HasMovedThisTurn = true;
        LastMoveAt = DateTime.UtcNow;
    }

    private void ValidateTurn(Guid playerId)
    {
        if (Status != MatchStatus.InProgress) throw new InvalidOperationException("A partida não está em andamento.");
        if (IsFinishedOrTimeout()) throw new InvalidOperationException("Partida finalizada ou tempo esgotado.");

        if (playerId != Guid.Empty && playerId != CurrentTurnPlayerId)
            throw new InvalidOperationException("Não é o seu turno.");

        if (DateTime.UtcNow.Subtract(LastMoveAt).TotalSeconds > 31) SwitchTurn();
    }

    private bool IsFinishedOrTimeout()
    {
        return Status == MatchStatus.Finished;
    }

    private void SwitchTurn()
    {
        CurrentTurnPlayerId = CurrentTurnPlayerId == Player1Id
            ? Player2Id ?? Guid.Empty
            : Player1Id;

        HasMovedThisTurn = false;
    }

    private void FinishGame(Guid winnerId)
    {
        Status = MatchStatus.Finished;
        WinnerId = winnerId;
        FinishedAt = DateTime.UtcNow;
        CurrentTurnPlayerId = Guid.Empty;
    }
}