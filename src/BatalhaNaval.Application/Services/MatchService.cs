using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Application.Interfaces;
using BatalhaNaval.Application.Services.AI;
using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Enums;
using BatalhaNaval.Domain.Exceptions;
using BatalhaNaval.Domain.Interfaces;
using BatalhaNaval.Domain.ValueObjects;

namespace BatalhaNaval.Application.Services;

public class MatchService : IMatchService
{
    private readonly ICacheService _cacheService;
    private readonly IMatchRepository _repository;
    private readonly IUserRepository _userRepository;

    public MatchService(IMatchRepository repository, IUserRepository userRepository, ICacheService cacheService)
    {
        _repository = repository;
        _userRepository = userRepository;
        _cacheService = cacheService;
    }

    public async Task<Guid> StartMatchAsync(StartMatchInput input, Guid playerId)
    {
        var playerExists = await _userRepository.ExistsAsync(playerId);

        if (!playerExists) throw new KeyNotFoundException($"O Jogador com ID '{playerId}' não foi encontrado.");

        if (input.OpponentId.HasValue && input.AiDifficulty.HasValue)
            throw new ArgumentException(
                "Não é possível definir um oponente humano e uma dificuldade de IA ao mesmo tempo.");

        if (input.OpponentId.HasValue)
        {
            if (playerId == input.OpponentId.Value)
                throw new ArgumentException("O jogador não pode jogar contra si mesmo.");

            var opponentExists = await _userRepository.ExistsAsync(input.OpponentId.Value);
            if (!opponentExists)
                throw new KeyNotFoundException($"O Oponente com ID '{input.OpponentId.Value}' não foi encontrado.");
        }

        // Verificar se existem partidas em curso antes de criar uma nova
        var activeMatchId = await _repository.GetActiveMatchIdAsync(playerId);
        if (activeMatchId.HasValue)
            throw new UserHasActiveMatchException(activeMatchId.Value);

        if (input.OpponentId.HasValue)
        {
            var opponentActiveMatchId = await _repository.GetActiveMatchIdAsync(input.OpponentId.Value);
            if (opponentActiveMatchId.HasValue)
                throw new OpponentBusyException($"O oponente '{input.OpponentId}' já está em outra partida.");
        }

        // Cria a partida (Entidade de Domínio)
        var match = new Match(playerId, input.Mode, input.AiDifficulty, input.OpponentId);

        await _repository.SaveAsync(match);
        return match.Id;
    }

    public async Task SetupShipsAsync(PlaceShipsInput input, Guid playerId)
    {
        var match = await GetMatchOrThrow(input.MatchId);

        if (match.Status != MatchStatus.Setup)
            throw new InvalidOperationException(match.Status == MatchStatus.Finished
                ? "Partida já encerrada."
                : "Partida em andamento.");

        var board = playerId == match.Player1Id ? match.Player1Board : match.Player2Board;

        // Limpa navios anteriores se houver (para permitir reset no setup)
        board.Ships.Clear();

        // 1. Posiciona os navios do Jogador
        foreach (var shipDto in input.Ships)
        {
            // Cria coordenadas baseadas na posição inicial e orientação
            var coordinates = GenerateCoordinates(shipDto.StartX, shipDto.StartY, shipDto.Size, shipDto.Orientation);

            var ship = new Ship(shipDto.Name, shipDto.Size, coordinates, shipDto.Orientation);
            board.AddShip(ship);
        }

        // Marca o jogador como pronto
        match.SetPlayerReady(playerId);

        // 2. Se for contra IA e o P1 estiver pronto, a IA monta o tabuleiro dela agora
        if (match.Player2Id == null && playerId == match.Player1Id)
        {
            SetupAiBoard(match.Player2Board);
            match.SetPlayerReady(Guid.Empty); // Guid.Empty representa a IA
        }

        var matchRedis = GenerateMatchRedis(match);
        var matchHistoryRedis = new List<MatchHistoryRedis>();

        await _cacheService.SetAsync($"match:{match.Id}", matchRedis, TimeSpan.FromMinutes(60));

        await _cacheService.SetAsync($"match:{match.Id}:history", matchHistoryRedis, TimeSpan.FromMinutes(60));

        await _repository.SaveAsync(match);
    }

    public async Task<TurnResultDto> ExecutePlayerShotAsync(ShootInput input, Guid playerId)
    {
        var match = await GetMatchOrThrow(input.MatchId);
        // TODO Adaptar para trabalhar com o cache em partida
        var matchRedis = await GetMatchRedisOrThrow(input.MatchId);

        // 1. Executa o tiro do jogador (Entidade Match valida turno e regras)
        var isHit = match.ExecuteShot(playerId, input.X, input.Y);

        // Verifica estado pós-tiro
        var isSunk = false; // Precisaríamos verificar se o tiro afundou algo específico, 
        // mas para o retorno simples, vamos focar no estado geral ou checar o grid.
        // Otimização: O Board poderia retornar metadata do tiro, mas vamos inferir.

        var targetBoard = playerId == match.Player1Id ? match.Player2Board : match.Player1Board;
        // Se acertou, verificamos se o navio naquela posição afundou agora
        if (isHit)
        {
            var hitShip =
                targetBoard.Ships.FirstOrDefault(s => s.Coordinates.Any(c => c.X == input.X && c.Y == input.Y));
            if (hitShip != null && hitShip.IsSunk) isSunk = true;
        }

        // 2. Salva o estado após jogada humana
        await _repository.SaveAsync(match);

        // 3. Se o jogo acabou com esse tiro, retorna
        if (match.IsFinished)
        {
            await ProcessEndGameAsync(match);
            return new TurnResultDto(isHit, isSunk, true, match.WinnerId, "Jogo Finalizado!");
        }

        // 4. TURNO DA IA (Automático)
        // Se for contra IA, e o turno mudou para o Player 2 (IA), ela deve jogar
        if (match.Player2Id == null && match.CurrentTurnPlayerId == Guid.Empty) await ProcessAiTurnLoopAsync(match);

        return new TurnResultDto(isHit, isSunk, match.IsFinished, match.WinnerId, isHit ? "Acertou!" : "Água.");
    }

    public async Task ExecutePlayerMoveAsync(MoveShipInput input)
    {
        var match = await GetMatchOrThrow(input.MatchId);
        // TODO Adaptar para trabalhar com o cache em partida
        var matchRedis = await GetMatchRedisOrThrow(input.MatchId);

        // Executa movimento (Entidade valida regras do Modo Dinâmico)
        match.ExecuteShipMovement(input.PlayerId, input.ShipId, input.Direction);

        await _repository.SaveAsync(match);

        // Se for contra IA, o movimento passa a vez. A IA deve responder.
        if (match.Player2Id == null && match.CurrentTurnPlayerId == Guid.Empty) await ProcessAiTurnLoopAsync(match);
    }

    public async Task CancelMatchAsync(Guid matchId, Guid playerId)
    {
        var match = await _repository.GetByIdAsync(matchId);

        if (match == null) throw new KeyNotFoundException($"Partida {matchId} não encontrada.");

        if (match.Player1Id != playerId && match.Player2Id != playerId)
            throw new UnauthorizedAccessException("O jogador não participa desta partida.");

        if (match.Status == MatchStatus.Finished)
            throw new InvalidOperationException("Esta partida já foi finalizada.");

        // CENÁRIO A: Partida em Configuração (Setup) -> Sem ônus
        if (match.Status == MatchStatus.Setup)
        {
            // Remove do banco
            await _repository.DeleteAsync(match);
            return;
        }

        // CENÁRIO B: Partida em Andamento (InProgress) -> Com ônus
        match.Status = MatchStatus.Finished;
        match.FinishedAt = DateTime.UtcNow;

        // Define o vencedor (quem NÃO cancelou)
        if (match.Player1Id == playerId)
            // P2 vence. Se P2 for NULL (IA), o WinnerId fica NULL
            match.WinnerId = match.Player2Id;
        // TODO Verificar se precisa realmente do else, pois quem chamou é quem cancelou
        else
            // P1 vence (alguém cancelou).
            match.WinnerId = match.Player1Id;

        // TODO: Calcular Ranking para o Vencedor e Perdedor
        // TODO: Atualizar estatísticas de Vitórias/Derrotas em PlayerProfile
        // TODO: Verificar conquistas de medalhas

        await _repository.UpdateAsync(match);
    }

    // --- MÉTODOS PRIVADOS AUXILIARES ---

    private async Task<Match> GetMatchOrThrow(Guid matchId)
    {
        var match = await _repository.GetByIdAsync(matchId);
        if (match == null) throw new KeyNotFoundException("Partida não encontrada.");
        return match;
    }

    private async Task<MatchRedis> GetMatchRedisOrThrow(Guid matchId)
    {
        var match = await _cacheService.GetAsync<MatchRedis>($"match:{matchId}");
        if (match == null) throw new KeyNotFoundException("Partida não encontrada no cache.");
        return match;
    }

    private async Task ProcessAiTurnLoopAsync(Match match)
    {
        // Loop da IA: Ela continua jogando enquanto acertar (regras oficiais)
        while (match.CurrentTurnPlayerId == Guid.Empty && !match.IsFinished)
        {
            // 1. Seleciona a estratégia baseada na dificuldade
            IAiStrategy strategy = match.AiDifficulty switch
            {
                Difficulty.Advanced => new AdvancedAiStrategy(),
                Difficulty.Intermediate => new IntermediateAiStrategy(),
                _ => new BasicAiStrategy()
            };

            // 2. Escolhe o alvo
            var target = strategy.ChooseTarget(match.Player1Board);

            // 3. Executa o tiro (Guid.Empty é o ID da IA na lógica interna do Match)
            // Nota: Adicionamos um pequeno delay artificial? Não no backend, o front cuida da animação.
            var aiHit = match.ExecuteShot(Guid.Empty, target.X, target.Y);

            if (match.IsFinished)
            {
                await ProcessEndGameAsync(match);
                break;
            }

            // Se a IA errou, o ExecuteShot já passou a vez para o Player1, e o loop while vai parar.
            // Se a IA acertou, o ExecuteShot manteve a vez nela, e o loop continua.
        }

        await _repository.SaveAsync(match);
    }

    private async Task ProcessEndGameAsync(Match match)
    {
        if (match.WinnerId != null)
        {
            var winnerProfile = await _repository.GetUserProfileAsync(match.WinnerId.Value);
            if (winnerProfile != null)
            {
                // Pontuação simples: 100 pts por vitória
                winnerProfile.AddWin(100);

                // Lógica de Medalhas poderia vir aqui (Ex: checar se venceu sem perder navios)
                if (match.WinnerId == match.Player1Id && !match.Player1Board.Ships.Any(s => s.IsSunk))
                    if (!winnerProfile.EarnedMedalCodes.Contains("ADMIRAL"))
                        winnerProfile.EarnedMedalCodes.Add("ADMIRAL");

                await _repository.UpdateUserProfileAsync(winnerProfile);
            }

            // Atualiza perdedor (se for humano)
            var loserId = match.WinnerId == match.Player1Id ? match.Player2Id : match.Player1Id;
            if (loserId != null && loserId != Guid.Empty)
            {
                var loserProfile = await _repository.GetUserProfileAsync(loserId.Value);
                if (loserProfile != null)
                {
                    loserProfile.AddLoss();
                    await _repository.UpdateUserProfileAsync(loserProfile);
                }
            }
        }
    }

    private void SetupAiBoard(Board aiBoard)
    {
        // Configuração Padrão da Frota
        var fleetSpecs = new List<(string Name, int Size)>
        {
            ("Porta-Aviões", 5),
            ("Encouraçado", 4),
            ("Submarino", 3),
            ("Destroyer", 3),
            ("Patrulha", 2)
        };

        var random = new Random();

        foreach (var (name, size) in fleetSpecs)
        {
            var placed = false;
            var attempts = 0;

            while (!placed && attempts < 100)
            {
                var orientation = random.Next(2) == 0 ? ShipOrientation.Horizontal : ShipOrientation.Vertical;
                var x = random.Next(Board.Size);
                var y = random.Next(Board.Size);

                try
                {
                    var coords = GenerateCoordinates(x, y, size, orientation);
                    var ship = new Ship(name, size, coords, orientation);
                    aiBoard.AddShip(ship); // Isso lança exceção se colidir ou sair do mapa
                    placed = true;
                }
                catch
                {
                    // Tenta novamente
                    attempts++;
                }
            }
        }
    }

    private List<Coordinate> GenerateCoordinates(int startX, int startY, int size, ShipOrientation orientation)
    {
        var coords = new List<Coordinate>();
        for (var i = 0; i < size; i++)
        {
            var x = orientation == ShipOrientation.Horizontal ? startX + i : startX;
            var y = orientation == ShipOrientation.Vertical ? startY + i : startY;
            coords.Add(new Coordinate(x, y));
        }

        return coords;
    }

    private MatchRedis GenerateMatchRedis(Match match)
    {
        var p1Ships = GenerateShipsMatchRedis(match.Player1Board.Ships);
        var p2Ships = GenerateShipsMatchRedis(match.Player2Board.Ships);

        var matchRedis = new MatchRedis
        {
            // IMUTÁVEIS
            MatchId = match.Id.ToString(),

            GameMode = match.Mode switch
            {
                GameMode.Classic => GameModeRedis.CLASSIC,
                _ => GameModeRedis.DYNAMIC
            },

            AiDifficulty = match.AiDifficulty switch
            {
                Difficulty.Basic => AiDifficultyRedis.BASIC,
                Difficulty.Advanced => AiDifficultyRedis.ADVANCED,
                _ => AiDifficultyRedis.INTERMEDIATE
            },

            Player1Id = match.Player1Id.ToString(),
            Player2Id = match.Player2Id?.ToString(),
            Status = MatchStatusRedis.IN_PROGRESS,

            // MUTÁVEIS
            TurnNumber = 0,
            TurnPlayerId = match.CurrentTurnPlayerId.ToString(),
            P1_ConsecutiveTimeouts = 0,
            P2_ConsecutiveTimeouts = 0,

            TurnStartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),

            P1_Stats = new PlayerStatsRedis(),
            P2_Stats = new PlayerStatsRedis(),

            Boards = new MatchBoardsRedis
            {
                P1 = new PlayerBoardRedis
                {
                    AliveShips = p1Ships.Count(s => !s.Sunk),
                    OceanGrid = new Dictionary<string, int>(),
                    Ships = p1Ships
                },
                P2 = new PlayerBoardRedis
                {
                    AliveShips = p2Ships.Count(s => !s.Sunk),
                    OceanGrid = new Dictionary<string, int>(),
                    Ships = p2Ships
                }
            }
        };

        return matchRedis;
    }

    private List<ShipRedis> GenerateShipsMatchRedis(List<Ship> ships)
    {
        return
        [
            .. ships.Select((ship, index) => new ShipRedis
            {
                Id = index + 1,
                Type = ship.Name,
                Size = ship.Size,
                Orientation = GetOrientationRedis(ship),
                Sunk = ship.IsSunk,
                IsDamaged = ship.HasBeenHit,
                Segments =
                [
                    .. ship.Coordinates.Select(c => new ShipSegmentRedis
                    {
                        X = c.X,
                        Y = c.Y,
                        Hit = c.IsHit
                    })
                ]
            })
        ];
    }

    private ShipOrientationRedis GetOrientationRedis(Ship ship)
    {
        if (ship.Size == 1) return ShipOrientationRedis.MIXED;

        return ship.Orientation.ToString() switch
        {
            "Horizontal" => ShipOrientationRedis.HORIZONTAL,
            "Vertical" => ShipOrientationRedis.VERTICAL,
            _ => ShipOrientationRedis.HORIZONTAL
        };
    }
}