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
    private const int POINTS_PER_WIN = 100;
    private const int POINTS_PER_HIT = 10;
    private readonly ICacheService _cacheService;
    private readonly IMatchRepository _repository; // Postgres (Cold Storage)
    private readonly IMatchStateRepository _stateRepository; // Redis (Hot Storage)
    private readonly IUserRepository _userRepository;

    public MatchService(
        IMatchRepository repository,
        IUserRepository userRepository,
        ICacheService cacheService,
        IMatchStateRepository stateRepository)
    {
        _repository = repository;
        _userRepository = userRepository;
        _cacheService = cacheService;
        _stateRepository = stateRepository;
    }

    // 1. INÍCIO DA PARTIDA (Mantém uso do Banco SQL para criação)
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

        // Cria a partida no Postgres (Status = Setup)
        var match = new Match(playerId, input.Mode, input.AiDifficulty, input.OpponentId);
        await _repository.SaveAsync(match);
        return match.Id;
    }

    // 2. CONFIGURAÇÃO (Transição SQL -> Redis)
    public async Task SetupShipsAsync(PlaceShipsInput input, Guid playerId)
    {
        // Setup ainda lê do SQL, pois o jogo não começou oficialmente no Cache
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
            var coordinates = GenerateCoordinates(shipDto.StartX, shipDto.StartY, shipDto.Size, shipDto.Orientation);
            var ship = new Ship(shipDto.Name, shipDto.Size, coordinates, shipDto.Orientation);
            board.AddShip(ship);
        }

        match.SetPlayerReady(playerId);

        // Se for contra IA, configura o tabuleiro dela
        if (match.Player2Id == null && playerId == match.Player1Id)
        {
            SetupAiBoard(match.Player2Board);
            match.SetPlayerReady(Guid.Empty);
        }

        // PERSISTÊNCIA HÍBRIDA:
        // 1. Salva no SQL (Para garantir consistência se o Redis cair agora)
        await _repository.SaveAsync(match);

        // 2. Se o jogo começou (Status mudou para InProgress no SetPlayerReady),
        // migramos o estado para o Redis. A partir de agora, lemos de lá.
        if (match.Status == MatchStatus.InProgress)
        {
            await _stateRepository.SaveStateAsync(match);
            // Kickstart da IA se for a vez dela ->  Nunca vai entrar aqui, pq mudei o starter pra player 1 começar contra a ia(ver regra q vai usar)
            if (match.Player2Id == null && match.CurrentTurnPlayerId == Guid.Empty) await ProcessAiTurnLoopAsync(match); 
        }
    }

    // 3. GAMEPLAY: TIRO (Redis First)
    public async Task<TurnResultDto> ExecutePlayerShotAsync(ShootInput input, Guid playerId)
    {
        // Tenta carregar do Redis primeiro (Rápido e Atualizado)
        var match = await _stateRepository.GetStateAsync(input.MatchId);

        // Fallback: Se não estiver no Redis (expirou?), tenta carregar do SQL
        if (match == null)
        {
            match = await _repository.GetByIdAsync(input.MatchId);
            if (match == null) throw new KeyNotFoundException("Partida não encontrada.");

            // Se estava no SQL mas não no Redis, e o jogo está rolando, salva no Redis de volta (Self-Healing)
            if (match.Status == MatchStatus.InProgress)
                await _stateRepository.SaveStateAsync(match);
        }

        // Executa lógica de domínio
        var isHit = match.ExecuteShot(playerId, input.X.Value, input.Y.Value);

        // Verifica se afundou (apenas visual para o DTO de retorno)
        var targetBoard = playerId == match.Player1Id ? match.Player2Board : match.Player1Board;
        var isSunk = false;
        if (isHit)
        {
            var hitShip =
                targetBoard.Ships.FirstOrDefault(s => s.Coordinates.Any(c => c.X == input.X && c.Y == input.Y));
            if (hitShip != null && hitShip.IsSunk) isSunk = true;
        }

        // SALVAMENTO:
        // 1. Redis (Obrigatório e Imediato)
        await _stateRepository.SaveStateAsync(match);

        // 2. SQL (comentado pra foco em performance)
        // Avaliar possibilidade de fazer um background job para ter performance + segurança
        // await _repository.SaveAsync(match); <-- Descomente se quiser segurança duplicada

        if (match.IsFinished)
        {
            await ProcessEndGameAsync(match);
            return new TurnResultDto(isHit, isSunk, true, match.WinnerId, "Jogo Finalizado!");
        }

        // Turno da IA
        if (match.Player2Id == null && match.CurrentTurnPlayerId == Guid.Empty) await ProcessAiTurnLoopAsync(match);
        // O objeto match é atualizado na memória dentro do método ProcessAiTurnLoopAsync (passagem de parametro por referencia)
        return new TurnResultDto(isHit, isSunk, match.IsFinished, match.WinnerId, isHit ? "Acertou!" : "Água.");
    }

// 4. GAMEPLAY: MOVIMENTO (Redis First)
    public async Task ExecutePlayerMoveAsync(MoveShipInput input, Guid playerId)
    {
        var match = await _stateRepository.GetStateAsync(input.MatchId);
        if (match == null) match = await GetMatchOrThrow(input.MatchId); // Fallback

        // Executa o movimento
        match.ExecuteShipMovement(playerId, input.ShipId, input.Direction.Value);

        // Verificamos se o turno caiu no colo da IA (por timeout)
        // Se sim, precisamos acordá-la para jogar imediatamente.
        if (match.Player2Id == null && match.CurrentTurnPlayerId == Guid.Empty) await ProcessAiTurnLoopAsync(match);

        await _stateRepository.SaveStateAsync(match);
        // await _repository.SaveAsync(match); // Opcional SQL
    }


    // 5. CONSULTA DE ESTADO (Redis First)
    public async Task<MatchGameStateDto> GetMatchStateAsync(Guid matchId, Guid playerId)
    {
        // Tenta pegar do Redis (muito rápido)
        var match = await _stateRepository.GetStateAsync(matchId);

        // Se falhar, pega do SQL (Fallback)
        if (match == null) match = await GetMatchOrThrow(matchId);

        if (match.Player1Id != playerId && match.Player2Id != playerId)
            throw new UnauthorizedAccessException("Você não tem permissão para visualizar esta partida.");

        var isPlayer1 = match.Player1Id == playerId;
        var myBoard = isPlayer1 ? match.Player1Board : match.Player2Board;
        var opponentBoard = isPlayer1 ? match.Player2Board : match.Player1Board;

        var stats = new MatchStatsDto(
            isPlayer1 ? match.Player1Hits : match.Player2Hits,
            isPlayer1 ? match.Player1ConsecutiveHits : match.Player2ConsecutiveHits,
            isPlayer1 ? match.Player2Hits : match.Player1Hits,
            isPlayer1 ? match.Player2ConsecutiveHits : match.Player1ConsecutiveHits
        );

        return new MatchGameStateDto(
            match.Id,
            match.Status,
            match.CurrentTurnPlayerId,
            match.CurrentTurnPlayerId == playerId,
            match.WinnerId,
            MapMyBoard(myBoard),
            MapOpponentBoard(opponentBoard),
            stats
        );
    }

    public async Task CancelMatchAsync(Guid matchId, Guid playerId)
    {
        // Carrega do SQL pois cancelamento é administrativo
        var match = await _repository.GetByIdAsync(matchId);
        if (match == null) throw new KeyNotFoundException($"Partida {matchId} não encontrada.");

        if (match.Player1Id != playerId && match.Player2Id != playerId)
            throw new UnauthorizedAccessException("O jogador não participa desta partida.");

        if (match.Status == MatchStatus.Finished)
            throw new InvalidOperationException("Esta partida já foi finalizada.");

        if (match.Status == MatchStatus.Setup)
        {
            await _repository.DeleteAsync(match);
            // Garante limpeza do cache se houver lixo
            await _stateRepository.DeleteStateAsync(matchId);
            return;
        }

        // InProgress -> Finished
        match.Status = MatchStatus.Finished;
        match.FinishedAt = DateTime.UtcNow;
        match.WinnerId = match.Player1Id == playerId ? match.Player2Id : match.Player1Id;

        await _repository.UpdateAsync(match); // Salva SQL
        await _stateRepository.DeleteStateAsync(matchId); // Mata o cache da partida
    }

    // 6. IA LOOP
    private async Task ProcessAiTurnLoopAsync(Match match)
    {
        while (match.CurrentTurnPlayerId == Guid.Empty && !match.IsFinished)
        {
            IAiStrategy strategy = match.AiDifficulty switch
            {
                Difficulty.Advanced => new AdvancedAiStrategy(),
                Difficulty.Intermediate => new IntermediateAiStrategy(),
                _ => new BasicAiStrategy()
            };

            var target = strategy.ChooseTarget(match.Player1Board);

            // Executa tiro na memória
            match.ExecuteShot(Guid.Empty, target.X, target.Y);

            if (match.IsFinished)
            {
                await ProcessEndGameAsync(match);
                break;
            }
        }

        // Salva estado final do turno da IA no Redis
        await _stateRepository.SaveStateAsync(match);
    }

    // 7. FIM DE JOGO (Persistência Final no SQL)
    private async Task ProcessEndGameAsync(Match match)
    {
        // 1. Salva estado final no Redis (Status Finished)
        await _stateRepository.SaveStateAsync(match);

        // 2. Atualiza SQL (Crucial para histórico eterno)
        await _repository.SaveAsync(match);

        // 3. Remove do Cache (Limpeza)
        // comentado pra deixar expirar sozinho (permite consulta pós-jogo imediata, mas "segura os dados no redis por 1h")
        // await _stateRepository.DeleteStateAsync(match.Id);

        // 4. Processa Pontos e Ranking (SQL)
        if (match.WinnerId.HasValue)
        {
            var winnerProfile = await _repository.GetUserProfileAsync(match.WinnerId.Value);
            if (winnerProfile != null)
            {
                var hits = match.WinnerId == match.Player1Id ? match.Player1Hits : match.Player2Hits;
                var totalWinPoints = POINTS_PER_WIN + hits * POINTS_PER_HIT;

                winnerProfile.AddWin(totalWinPoints);

                // Medalha Almirante (Flawless Victory)
                if (match.WinnerId == match.Player1Id && !match.Player1Board.Ships.Any(s => s.IsSunk))
                    if (!winnerProfile.EarnedMedalCodes.Contains("ADMIRAL"))
                        winnerProfile.EarnedMedalCodes.Add("ADMIRAL");

                await _repository.UpdateUserProfileAsync(winnerProfile);
            }

            var loserId = match.WinnerId == match.Player1Id ? match.Player2Id : match.Player1Id;
            if (loserId != null && loserId != Guid.Empty)
            {
                var loserProfile = await _repository.GetUserProfileAsync(loserId.Value);
                if (loserProfile != null)
                {
                    var hits = loserId == match.Player1Id ? match.Player1Hits : match.Player2Hits;
                    loserProfile.Losses++;
                    loserProfile.CurrentStreak = 0;
                    loserProfile.RankPoints += hits * POINTS_PER_HIT;
                    loserProfile.UpdatedAt = DateTime.UtcNow;
                    await _repository.UpdateUserProfileAsync(loserProfile);
                }
            }
        }

        await _cacheService.RemoveAsync("global_ranking");
    }

    // --- MÉTODOS PRIVADOS AUXILIARES ---

    private async Task<Match> GetMatchOrThrow(Guid matchId)
    {
        var match = await _repository.GetByIdAsync(matchId);
        if (match == null) throw new KeyNotFoundException("Partida não encontrada.");
        return match;
    }

    private void SetupAiBoard(Board aiBoard)
    {
        var fleetSpecs = new List<(string Name, int Size)>
        {
            ("Porta-Aviões", 6),
            ("Porta-Aviões", 6),
            ("Navio de Guerra", 4),
            ("Navio de Guerra", 4),
            ("Encouraçado", 3),
            ("Submarino", 1)
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
                    aiBoard.AddShip(ship);
                    placed = true;
                }
                catch
                {
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

    private BoardStateDto MapMyBoard(Board board)
    {
        var shipsDto = board.Ships.Select(s => new ShipDto(
            s.Id, s.Name, s.Size, s.IsSunk, s.Orientation,
            s.Coordinates.Select(c => new CoordinateDto(c.X, c.Y, c.IsHit)).ToList()
        )).ToList();
        return new BoardStateDto(board.Cells, shipsDto);
    }

    private BoardStateDto MapOpponentBoard(Board board)
    {
        var maskedGrid = board.Cells.Select(row =>
            row.Select(cell => cell == CellState.Ship ? CellState.Water : cell).ToList()
        ).ToList();

        var visibleShips = board.Ships
            .Where(s => s.IsSunk)
            .Select(s => new ShipDto(
                s.Id, s.Name, s.Size, s.IsSunk, s.Orientation,
                s.Coordinates.Select(c => new CoordinateDto(c.X, c.Y, c.IsHit)).ToList()
            ))
            .ToList();
        return new BoardStateDto(maskedGrid, visibleShips);
    }
}