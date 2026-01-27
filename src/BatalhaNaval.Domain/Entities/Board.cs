using BatalhaNaval.Domain.Enums;
using BatalhaNaval.Domain.ValueObjects;

namespace BatalhaNaval.Domain.Entities;

public class Board
{
    public const int Size = 10;
    public List<Ship> Ships { get; private set; } = new();
    public CellState[,] Cells { get; private set; } = new CellState[Size, Size];

    public Board()
    {
        // Inicializa o grid com Água
        for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
                Cells[i, j] = CellState.Water;
    }

    public void AddShip(Ship ship)
    {
        // Valida limites e colisão para posicionamento inicial
        ValidateCoordinatesOrThrow(ship.Coordinates, ship.Id);

        Ships.Add(ship);

        // Marca no Grid Visual
        foreach (var coord in ship.Coordinates)
        {
            Cells[coord.X, coord.Y] = CellState.Ship;
        }
    }

    public void MoveShip(Guid shipId, MoveDirection direction)
    {
        var ship = Ships.FirstOrDefault(s => s.Id == shipId);
        if (ship == null) 
            throw new KeyNotFoundException("Navio não encontrado neste tabuleiro.");

        // 1. Predição
        var proposedCoordinates = ship.PredictMovement(direction);

        // 2. Validação (Usa a mesma lógica do AddShip, mas ignora o próprio navio)
        ValidateCoordinatesOrThrow(proposedCoordinates, ship.Id);

        // 3. Limpeza visual da posição antiga
        foreach (var coord in ship.Coordinates)
        {
            Cells[coord.X, coord.Y] = CellState.Water; 
        }

        // 4. Confirmação
        ship.ConfirmMovement(proposedCoordinates);

        // 5. Atualização visual da nova posição
        foreach (var coord in ship.Coordinates)
        {
            Cells[coord.X, coord.Y] = CellState.Ship;
        }
    }

    private void ValidateCoordinatesOrThrow(List<Coordinate> coords, Guid ignoreShipId)
    {
        foreach (var coord in coords)
        {
            if (!coord.IsWithinBounds(Size))
                throw new InvalidOperationException("Coordenada fora dos limites do tabuleiro.");

            // Verifica se a célula já está ocupada por OUTRO navio
            var isOccupied = Ships.Any(otherShip => 
                otherShip.Id != ignoreShipId && 
                otherShip.Coordinates.Any(c => c.X == coord.X && c.Y == coord.Y));

            if (isOccupied)
                throw new InvalidOperationException("Coordenada já ocupada por outro navio.");
        }
    }

    public bool ReceiveShot(int x, int y)
    {
        // Se já foi atingido antes (Hit ou Missed), retorna false ou lança erro?
        // Game design: Geralmente apenas ignora ou avisa, aqui vamos considerar tiro inválido se repetido
        if (Cells[x, y] == CellState.Hit || Cells[x, y] == CellState.Missed)
             return false; // Tiro repetido não consome turno ou deve ser tratado acima

        var ship = Ships.FirstOrDefault(s => s.Coordinates.Any(c => c.X == x && c.Y == y));
        
        if (ship != null)
        {
            var coord = ship.Coordinates.First(c => c.X == x && c.Y == y);
            
            // Atualiza estado do navio (Objeto imutável Coordinate sendo substituído)
            var newCoords = new List<Coordinate>(ship.Coordinates);
            var index = newCoords.IndexOf(coord);
            newCoords[index] = coord with { IsHit = true };
            
            ship.UpdateDamage(newCoords); // Método auxiliar no Ship para atualizar dano sem mover

            Cells[x, y] = CellState.Hit;
            return true;
        }

        Cells[x, y] = CellState.Missed;
        return false;
    }
    
    public bool AllShipsSunk() => Ships.Count > 0 && Ships.All(s => s.IsSunk);
}