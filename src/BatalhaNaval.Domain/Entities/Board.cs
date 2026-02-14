using BatalhaNaval.Domain.Enums;
using BatalhaNaval.Domain.ValueObjects;

namespace BatalhaNaval.Domain.Entities;

public class Board
{
    public const int Size = 10;

    public Board()
    {
        // Inicializa a grade 10x10 com Água
        Cells = new List<List<CellState>>();
        for (var x = 0; x < Size; x++)
        {
            var row = new List<CellState>();
            for (var y = 0; y < Size; y++) row.Add(CellState.Water);
            Cells.Add(row);
        }
    }

    public List<Ship> Ships { get; } = new();

    // Representação visual do tabuleiro
    public List<List<CellState>> Cells { get; set; }

    public void AddShip(Ship ship)
    {
        // Validação inicial de posicionamento (Setup)
        ValidatePlacement(ship.Coordinates, ship.Id);
        Ships.Add(ship);

        foreach (var coord in ship.Coordinates)
            Cells[coord.X][coord.Y] = CellState.Ship;
    }

    public void MoveShip(Guid shipId, MoveDirection direction)
    {
        var ship = Ships.FirstOrDefault(s => s.Id == shipId);
        if (ship == null)
            throw new KeyNotFoundException($"Navio com ID {shipId} não encontrado neste tabuleiro.");

        // REGRA 1: Navio Avariado não move
        // Verifica se qualquer parte do navio já foi atingida
        if (ship.Coordinates.Any(c => c.IsHit))
            throw new InvalidOperationException("O navio está avariado e não pode ser movido.");

        // REGRA 2: Bloqueio de Strafing (Movimento Lateral)
        // Navios > 1 célula só movem no seu eixo
        if (ship.Size > 1)
        {
            var isVertical = ship.Orientation == ShipOrientation.Vertical;
            var isHorizontal = ship.Orientation == ShipOrientation.Horizontal;

            // Se Vertical, só permite North/South
            if (isVertical && (direction == MoveDirection.West || direction == MoveDirection.East))
                throw new InvalidOperationException(
                    $"O navio '{ship.Name}' (Vertical) só pode se mover para Norte ou Sul.");

            // Se Horizontal, só permite West/East
            if (isHorizontal && (direction == MoveDirection.North || direction == MoveDirection.South))
                throw new InvalidOperationException(
                    $"O navio '{ship.Name}' (Horizontal) só pode se mover para Leste ou Oeste.");
        }

        // Calcula as novas coordenadas (Previsão)
        // Usamos o método do Ship, mas precisamos garantir a conversão do Enum se necessário.
        // Como o seu Enum bate com o esperado (North/South...), podemos passar direto ou calcular aqui.
        // Vou calcular aqui para garantir total controle sobre o Enum do seu projeto.
        var deltaX = 0;
        var deltaY = 0;

        switch (direction)
        {
            case MoveDirection.North: deltaY = -1; break;
            case MoveDirection.South: deltaY = 1; break;
            case MoveDirection.East: deltaX = 1; break;
            case MoveDirection.West: deltaX = -1; break;
            default: throw new ArgumentException("Direção inválida.");
        }

        var newCoordinates = new List<Coordinate>();
        foreach (var c in ship.Coordinates) newCoordinates.Add(new Coordinate(c.X + deltaX, c.Y + deltaY, c.IsHit));

        // REGRA 3: Validação de Destino (Colisões e Tiros)
        ValidatePlacement(newCoordinates, ship.Id);

        // --- EXECUÇÃO DO MOVIMENTO ---

        // A. Limpa a posição antiga
        // CORREÇÃO DO "RASTRO FANTASMA":
        // Como validamos que navio avariado NÃO move, sabemos que ele estava intacto.
        // Porém, precisamos garantir que ao sair, a célula volte ao estado correto. Se a célula era SHIP, vira WATER
        foreach (var coord in ship.Coordinates)
            if (Cells[coord.X][coord.Y] == CellState.Ship)
                Cells[coord.X][coord.Y] = CellState.Water;

        // Se fosse Hit (o que a regra 1 impede), viraria Missed:
        // else if (Cells[coord.X][coord.Y] == CellState.Hit) Cells[coord.X][coord.Y] = CellState.Missed;
        // B. Atualiza a entidade Navio
        // (Assumindo que você tem o método ConfirmMovement ou similar no Ship.cs)
        // Se não tiver, use: ship.Coordinates = newCoordinates; (se for acessível) ou o método UpdateCoordinates
        ship.ConfirmMovement(newCoordinates);

        // C. Pinta a nova posição
        foreach (var coord in newCoordinates) Cells[coord.X][coord.Y] = CellState.Ship;
    }

    // Método auxiliar unificado para AddShip e MoveShip
    private void ValidatePlacement(List<Coordinate> coords, Guid ignoreShipId)
    {
        foreach (var coord in coords)
        {
            // Validação A: Limites do Mapa
            if (coord.X < 0 || coord.X >= Size || coord.Y < 0 || coord.Y >= Size)
                throw new InvalidOperationException("O movimento faria o navio sair dos limites do tabuleiro.");

            // Validação B: Colisão com Objetos do Cenário (Tiros/Destroços)
            // O navio não pode entrar em uma célula que já tem Hit ou Missed
            var currentCellState = Cells[coord.X][coord.Y];
            if (currentCellState == CellState.Hit || currentCellState == CellState.Missed)
                throw new InvalidOperationException(
                    $"Movimento bloqueado: A posição ({coord.X}, {coord.Y}) já foi alvejada.");

            // Validação C: Colisão com Outros Navios
            // Verifica se a coordenada bate em algum navio (ignorando o próprio navio que está se movendo)
            // Se a célula é SHIP e não pertence ao navio atual -> Colisão
            var isOccupiedByAnotherShip = Ships.Any(otherShip =>
                otherShip.Id != ignoreShipId &&
                otherShip.Coordinates.Any(c => c.X == coord.X && c.Y == coord.Y));

            if (isOccupiedByAnotherShip)
                throw new InvalidOperationException(
                    $"Colisão detectada com outro navio na posição ({coord.X}, {coord.Y}).");
        }
    }

    public bool ReceiveShot(int x, int y)
    {
        // 1. Validação de Limites
        if (x < 0 || x >= Size || y < 0 || y >= Size)
            throw new InvalidOperationException($"Coordenada ({x}, {y}) está fora dos limites do tabuleiro.");

        // 2. Validação de Tiro Repetido
        var currentCell = Cells[x][y];
        if (currentCell == CellState.Hit || currentCell == CellState.Missed)
            // Se quiser apenas retornar false (tiro inválido mas não erro de sistema), remova o throw.
            throw new InvalidOperationException($"A posição ({x}, {y}) já foi alvejada previamente.");

        // 3. Verifica se acertou Navio
        var ship = Ships.FirstOrDefault(s => s.Coordinates.Any(c => c.X == x && c.Y == y));

        if (ship != null)
        {
            // Encontra a coordenada específica dentro do navio
            var coordIndex = ship.Coordinates.FindIndex(c => c.X == x && c.Y == y);
            if (coordIndex >= 0)
            {
                // Atualiza o estado de dano do navio (IsHit = true)
                // Criamos uma nova lista para garantir imutabilidade se necessário, ou alteramos direto
                var newCoords = new List<Coordinate>(ship.Coordinates);

                // Usamos o 'with' se Coordinate for record, ou cria novo objeto se for class
                var oldCoord = newCoords[coordIndex];
                newCoords[coordIndex] = new Coordinate(oldCoord.X, oldCoord.Y, true);

                ship.UpdateDamage(newCoords);
            }

            // Atualiza o visual do tabuleiro
            Cells[x][y] = CellState.Hit;
            return true; // Acertou
        }

        // 4. Se não acertou nada (Água)
        Cells[x][y] = CellState.Missed;
        return false; // Errou
    }

    public bool AllShipsSunk()
    {
        return Ships.Count > 0 && Ships.All(s => s.IsSunk);
    }
}