using BatalhaNaval.Domain.Enums;
using BatalhaNaval.Domain.ValueObjects;

namespace BatalhaNaval.Domain.Entities;

public class Match
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid Player1Id { get; private set; }
    public Guid? Player2Id { get; private set; }
    public Board Player1Board { get; private set; }
    public Board Player2Board { get; private set; }
    public GameMode Mode { get; private set; }
    public Difficulty? AiDifficulty { get; private set; }
    public MatchStatus Status { get; private set; }
    
    public Guid CurrentTurnPlayerId { get; private set; }
    public Guid? WinnerId { get; private set; }
    
    public DateTime StartedAt { get; private set; } // Quando o status muda para InProgress
    public DateTime LastMoveAt { get; private set; }

    // Controle de prontidão
    private bool _player1Ready = false;
    private bool _player2Ready = false; // Se for IA, já começa true ou logicamente tratado

    public Match(Guid player1Id, GameMode mode, Difficulty? aiDifficulty = null, Guid? player2Id = null)
    {
        Player1Id = player1Id;
        Player2Id = player2Id;
        Mode = mode;
        AiDifficulty = aiDifficulty;
        Status = MatchStatus.Setup;
        
        Player1Board = new Board();
        Player2Board = new Board();
        
        CurrentTurnPlayerId = player1Id; // P1 começa o setup, ou aleatório no início do jogo
    }

    // Método chamado quando o jogador termina de posicionar navios
    public void SetPlayerReady(Guid playerId)
    {
        if (Status != MatchStatus.Setup) return;

        if (playerId == Player1Id) _player1Ready = true;
        else if (playerId == Player2Id) _player2Ready = true;

        // Verifica se pode iniciar o jogo
        bool isAiGame = Player2Id == null;
        if (_player1Ready && (_player2Ready || isAiGame))
        {
            StartGame();
        }
    }

    private void StartGame()
    {
        Status = MatchStatus.InProgress;
        StartedAt = DateTime.UtcNow;
        LastMoveAt = DateTime.UtcNow;
        // P1 sempre começa atirando? Ou random? Vamos assumir P1 por padrão.
        CurrentTurnPlayerId = Player1Id;
    }

    // Ação 1: Atirar
    public bool ExecuteShot(Guid playerId, int x, int y)
    {
        ValidateTurn(playerId);

        var targetBoard = (playerId == Player1Id) ? Player2Board : Player1Board;
        
        // Verifica se o tiro é válido (não repetido)
        // Se retornar false (tiro repetido), não troca o turno, apenas avisa (tratamento na App Layer)
        bool result = targetBoard.ReceiveShot(x, y);

        // Se acertou água (CellState.Missed), passa a vez.
        // Se acertou navio (CellState.Hit), mantém a vez (Regra: "Caso acerte... pode jogar outra bomba")
        var cellState = targetBoard.Cells[x, y];
        bool hitShip = cellState == CellState.Hit;

        if (targetBoard.AllShipsSunk())
        {
            FinishGame(playerId);
        }
        else if (!hitShip)
        {
            SwitchTurn();
        }

        LastMoveAt = DateTime.UtcNow;
        return hitShip;
    }

    // Ação 2: Mover Navio (Apenas Modo Dinâmico)
    public void ExecuteShipMovement(Guid playerId, Guid shipId, MoveDirection direction)
    {
        if (Mode != GameMode.Dynamic)
            throw new InvalidOperationException("Movimentação de navios só é permitida no modo Dinâmico.");

        ValidateTurn(playerId);

        var myBoard = (playerId == Player1Id) ? Player1Board : Player2Board;
        
        // Tenta mover. Se falhar (colisão/navio atingido), o Board lança exceção e o turno NÃO muda.
        myBoard.MoveShip(shipId, direction);

        // Se mover com sucesso, o turno ACABA imediatamente (ao contrário do tiro que pode repetir)
        SwitchTurn();
        LastMoveAt = DateTime.UtcNow;
    }

    private void ValidateTurn(Guid playerId)
    {
        if (Status != MatchStatus.InProgress) throw new InvalidOperationException("A partida não está em andamento.");
        if (IsFinishedOrTimeout()) throw new InvalidOperationException("Partida finalizada ou tempo esgotado.");
        if (playerId != CurrentTurnPlayerId) throw new InvalidOperationException("Não é o seu turno.");
        
        // Validação de tempo (30s)
        if (DateTime.UtcNow.Subtract(LastMoveAt).TotalSeconds > 31)
        {
            SwitchTurn();
            throw new TimeoutException("Tempo de jogada esgotado. Vez passada.");
        }
    }

    private bool IsFinishedOrTimeout() => Status == MatchStatus.Finished;

    private void SwitchTurn()
    {
        CurrentTurnPlayerId = (CurrentTurnPlayerId == Player1Id) 
            ? (Player2Id ?? Guid.Empty) 
            : Player1Id;
    }

    private void FinishGame(Guid winnerId)
    {
        Status = MatchStatus.Finished;
        WinnerId = winnerId;
    }
}