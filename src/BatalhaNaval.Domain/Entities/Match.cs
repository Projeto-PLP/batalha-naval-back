using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using BatalhaNaval.Domain.Enums;
using BatalhaNaval.Domain.Exceptions;
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
    
    //TOD0: Verificar estado no banco no match configuration, se vai mandar pro db
    [Column(name:"player1_misses")] public int Player1Misses { get; set; } 
    [Column(name:"player2_misses")] public int Player2Misses { get; set; }

    [Column("has_moved_this_turn")] public bool HasMovedThisTurn { get; set; }

    // Contadores de timeouts consecutivos (reseta quando o jogador age)
    // Persistidos apenas no Redis (via ToRedisDto/FromRedisDto), não no SQL.
    [NotMapped] public int Player1ConsecutiveTimeouts { get; set; }
    [NotMapped] public int Player2ConsecutiveTimeouts { get; set; }

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
    public Guid CurrentTurnPlayerId { get; set; }

    [Description("Identificador único do vencedor")]
    public Guid? WinnerId { get; set; }

    [Description("Data e hora de início da partida")]
    public DateTime StartedAt { get; set; }

    [Description("Data e hora do último movimento")]
    public DateTime LastMoveAt { get; set; }

    // ====================================================================
    // MÉTODOS DE SUPORTE AO REDIS (MAPPING)
    // ====================================================================

    public MatchRedis ToRedisDto()
    {
        return new MatchRedis
        {   
            StartedAt = new DateTimeOffset(StartedAt).ToUnixTimeSeconds(),
            MatchId = Id.ToString(),
            Player1Id = Player1Id.ToString(),
            Player2Id = Player2Id?.ToString(),
            GameMode = MapGameModeToRedis(Mode),
            AiDifficulty = AiDifficulty.HasValue ? MapDifficultyToRedis(AiDifficulty.Value) : null,
            Status = MapStatusToRedis(Status),

            TurnPlayerId = CurrentTurnPlayerId.ToString(),
            TurnStartedAt = new DateTimeOffset(LastMoveAt).ToUnixTimeSeconds(),

            P1_ConsecutiveTimeouts = Player1ConsecutiveTimeouts,
            P2_ConsecutiveTimeouts = Player2ConsecutiveTimeouts,

            // Mapeia Stats
            P1_Stats = new PlayerStatsRedis
            {
                Hits = Player1Hits,
                Streak = Player1ConsecutiveHits,
                Misses = Player1Misses
            },
            P2_Stats = new PlayerStatsRedis
            {
                Hits = Player2Hits,
                Streak = Player2ConsecutiveHits,
                Misses = Player2Misses

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
        match.StartedAt = DateTimeOffset.FromUnixTimeSeconds(dto.StartedAt).UtcDateTime;

        // Timeouts consecutivos
        match.Player1ConsecutiveTimeouts = dto.P1_ConsecutiveTimeouts;
        match.Player2ConsecutiveTimeouts = dto.P2_ConsecutiveTimeouts;
        
        // Stats
        match.Player1Hits = dto.P1_Stats.Hits;
        match.Player1ConsecutiveHits = dto.P1_Stats.Streak;
        match.Player1Misses = dto.P1_Stats.Misses;
        match.Player2Hits = dto.P2_Stats.Hits;
        match.Player2ConsecutiveHits = dto.P2_Stats.Streak;
        match.Player2Misses = dto.P2_Stats.Misses;

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

        redisBoard.ShotHistory = board.ShotHistory.Select(shot => new ShotLogRedis
        {
            X = shot.X,
            Y = shot.Y,
            Hit = shot.IsHit
        }).ToList();

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

        foreach (var shotDto in dto.ShotHistory)
        {
            board.ShotHistory.Add(new Coordinate(shotDto.X, shotDto.Y) { IsHit = shotDto.Hit });
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

        if (starter == 0 || Player2Id == null) // Player 1 começa se é jogo contra IA
            CurrentTurnPlayerId = Player1Id;
        else
            CurrentTurnPlayerId = (Guid)Player2Id;

        HasMovedThisTurn = false;
    }

    public bool ExecuteShot(Guid playerId, int x, int y)
    {
        if (CheckAndApplyTimeout())
        {
            // Se o jogo acabou por inatividade (4 timeouts), lança exceção diferente
            if (Status == MatchStatus.Finished)
                throw new InvalidOperationException("Partida encerrada por inatividade.");

            throw new TurnTimeoutException(
                "Tempo esgotado! O oponente demorou mais de 31 segundos e o turno passou para você.");
        }

        ValidateTurn(playerId);

        // Jogador agiu com sucesso — reseta o contador de timeouts consecutivos dele
        ResetConsecutiveTimeouts(playerId);

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
            if (playerId == Player1Id)
            {
                Player1ConsecutiveHits = 0;
                Player1Misses++;
            }
            else
            {
                Player2ConsecutiveHits = 0;
                Player2Misses++;
            }
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

        if (CheckAndApplyTimeout())
        {
            if (Status == MatchStatus.Finished)
                throw new InvalidOperationException("Partida encerrada por inatividade.");

            throw new TurnTimeoutException(
                "Tempo esgotado! O oponente demorou mais de 31 segundos e o turno passou para você.");
        }

        ValidateTurn(playerId);

        if (HasMovedThisTurn)
            throw new InvalidOperationException("Você já realizou um movimento neste turno. Agora deve atirar.");

        // Jogador agiu com sucesso — reseta o contador de timeouts consecutivos dele
        ResetConsecutiveTimeouts(playerId);

        var myBoard = playerId == Player1Id ? Player1Board : Player2Board;

        myBoard.MoveShip(shipId, direction);

        HasMovedThisTurn = true;
        LastMoveAt = DateTime.UtcNow;
    }

    // ====================================================================
    // MÉTODOS PRIVADOS DE CONTROLE DE TURNO E TEMPO
    // ====================================================================

    // Chamado pelo endpoint de polling para aplicar timeout automático sem ação do jogador.
    // Retorna true se o turno foi trocado (ou jogo encerrado por inatividade), false se ainda está no prazo.
    public bool ApplyTimeoutIfExpired()
    {
        if (Status != MatchStatus.InProgress) return false;
        if (DateTime.UtcNow.Subtract(LastMoveAt).TotalSeconds <= 31) return false;

        // Contabiliza o timeout para quem deixou o tempo estourar
        IncrementTimeoutAndCheckInactivity(CurrentTurnPlayerId);

        // Se atingiu 4 timeouts consecutivos, o jogo já foi encerrado por FinishByInactivity
        if (Status == MatchStatus.Finished) return true;

        SwitchTurn();
        LastMoveAt = DateTime.UtcNow;
        return true;
    }

    private bool CheckAndApplyTimeout()
    {
        if (Status != MatchStatus.InProgress) return false;
        if (DateTime.UtcNow.Subtract(LastMoveAt).TotalSeconds <= 31) return false;

        // Tempo estourou — penaliza o dono do turno atual, não importa quem fez a requisição.
        // Isso impede que o jogador atrasado jogue impunemente após 31s.
        IncrementTimeoutAndCheckInactivity(CurrentTurnPlayerId);

        if (Status == MatchStatus.Finished) return true;

        SwitchTurn();
        LastMoveAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    ///     Incrementa o contador de timeouts consecutivos do jogador que deixou o tempo estourar.
    ///     Se atingir 4 timeouts consecutivos, encerra a partida por inatividade.
    /// </summary>
    private void IncrementTimeoutAndCheckInactivity(Guid timedOutPlayerId)
    {
        const int maxConsecutiveTimeouts = 4;

        if (timedOutPlayerId == Player1Id)
        {
            Player1ConsecutiveTimeouts++;
            if (Player1ConsecutiveTimeouts >= maxConsecutiveTimeouts)
                FinishByInactivity(Player2Id ?? Guid.Empty);
        }
        else
        {
            Player2ConsecutiveTimeouts++;
            if (Player2ConsecutiveTimeouts >= maxConsecutiveTimeouts)
                FinishByInactivity(Player1Id);
        }
    }

    /// <summary>
    ///     Reseta o contador de timeouts consecutivos quando o jogador efetivamente joga.
    /// </summary>
    private void ResetConsecutiveTimeouts(Guid playerId)
    {
        if (playerId == Player1Id)
            Player1ConsecutiveTimeouts = 0;
        else
            Player2ConsecutiveTimeouts = 0;
    }

    private void ValidateTurn(Guid playerId)
    {
        if (Status != MatchStatus.InProgress) throw new InvalidOperationException("A partida não está em andamento.");
        if (IsFinishedOrTimeout()) throw new InvalidOperationException("Partida finalizada ou tempo esgotado.");

        // Removemos a exceção do Guid.Empty. A IA agora obedece estritamente à regra de turno.
        if (playerId != CurrentTurnPlayerId)
            throw new InvalidOperationException("Não é o seu turno.");
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

    private void FinishByInactivity(Guid winnerId)
    {
        Status = MatchStatus.Finished;
        WinnerId = winnerId == Guid.Empty ? null : winnerId;
        FinishedAt = DateTime.UtcNow;
        CurrentTurnPlayerId = Guid.Empty;
    }

    private void FinishGame(Guid winnerId)
    {
        Status = MatchStatus.Finished;
        WinnerId = winnerId == Guid.Empty ? null : winnerId;
        FinishedAt = DateTime.UtcNow;
        CurrentTurnPlayerId = Guid.Empty;
    }
}