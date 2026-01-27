using BatalhaNaval.Domain.Enums;
using BatalhaNaval.Domain.ValueObjects;

namespace BatalhaNaval.Domain.Entities;

public class Ship
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; }
    public int Size { get; private set; }
    public ShipOrientation Orientation { get; private set; }
    public List<Coordinate> Coordinates { get; private set; }
    
    public bool IsSunk => Coordinates.All(c => c.IsHit);
    public bool HasBeenHit => Coordinates.Any(c => c.IsHit);

    public Ship(string name, int size, List<Coordinate> coordinates, ShipOrientation orientation)
    {
        if (coordinates.Count != size)
            throw new ArgumentException($"O navio {name} precisa de {size} coordenadas, mas recebeu {coordinates.Count}.");

        // Validação de Integridade (Opcional, mas recomendada para evitar navios "quebrados")
        // Poderíamos validar aqui se as coordenadas são contíguas (ex: X, X+1...)
        
        Name = name;
        Size = size;
        Coordinates = coordinates;
        Orientation = orientation;
    }

    public List<Coordinate> PredictMovement(MoveDirection direction)
    {
        if (HasBeenHit)
            throw new InvalidOperationException("Navios avariados não podem se mover.");

        // Regra: Navios > 1 só se movem no seu eixo
        if (Size > 1)
        {
            bool isMovingVertical = direction == MoveDirection.North || direction == MoveDirection.South;
            bool isMovingHorizontal = direction == MoveDirection.East || direction == MoveDirection.West;

            if (Orientation == ShipOrientation.Vertical && !isMovingVertical)
                throw new InvalidOperationException("Navios verticais só podem se mover para Norte ou Sul.");

            if (Orientation == ShipOrientation.Horizontal && !isMovingHorizontal)
                throw new InvalidOperationException("Navios horizontais só podem se mover para Leste ou Oeste.");
        }

        int deltaX = 0, deltaY = 0;
        switch (direction)
        {
            case MoveDirection.North: deltaY = -1; break;
            case MoveDirection.South: deltaY = 1; break;
            case MoveDirection.East:  deltaX = 1; break;
            case MoveDirection.West:  deltaX = -1; break;
        }

        return Coordinates.Select(c => new Coordinate(c.X + deltaX, c.Y + deltaY, c.IsHit)).ToList();
    }

    public void ConfirmMovement(List<Coordinate> newCoordinates)
    {
        if (newCoordinates.Count != Size)
            throw new InvalidOperationException("Erro crítico: Coordenadas de movimento inválidas.");
            
        Coordinates = newCoordinates;
    }

    // Novo método para registrar dano mantendo o encapsulamento
    public void UpdateDamage(List<Coordinate> updatedCoordinates)
    {
        if (updatedCoordinates.Count != Size) throw new ArgumentException("Tamanho inválido para atualização de dano.");
        Coordinates = updatedCoordinates;
    }
}