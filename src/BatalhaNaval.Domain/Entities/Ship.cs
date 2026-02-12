using System.Text.Json.Serialization;
using BatalhaNaval.Domain.Enums;
using BatalhaNaval.Domain.ValueObjects;

namespace BatalhaNaval.Domain.Entities;

public class Ship
{

    // CONSTRUTOR PARA SERIALIZADOR
    [JsonConstructor]
    private Ship() { }
    

    // Usado pelo Match.FromRedisDto
    public Ship(Guid id, string name, int size, List<Coordinate> coordinates, ShipOrientation orientation)
    {
        Id = id;
        Name = name;
        Size = size;
        Coordinates = coordinates;
        Orientation = orientation;
    }
    
    // Usado pelo MatchService 
    public Ship(string name, int size, List<Coordinate> coordinates, ShipOrientation orientation)
    {
        if (coordinates.Count != size)
            throw new ArgumentException(
                $"O navio {name} precisa de {size} coordenadas, mas recebeu {coordinates.Count}.");

        Id = Guid.NewGuid();
        Name = name;
        Size = size;
        Coordinates = coordinates;
        Orientation = orientation;
    }
    
    // PROPRIEDADES
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public int Size { get; private set; }
    public ShipOrientation Orientation { get; private set; }
    public List<Coordinate> Coordinates { get; private set; }

    public bool IsSunk => Coordinates != null && Coordinates.All(c => c.IsHit);
    public bool HasBeenHit => Coordinates != null && Coordinates.Any(c => c.IsHit);
    
    // MÉTODOS DE DOMÍNIO

    public List<Coordinate> PredictMovement(MoveDirection direction)
    {
        if (HasBeenHit)
            throw new InvalidOperationException("Navios avariados não podem se mover.");

        if (Size > 1)
        {
            var isMovingVertical = direction == MoveDirection.North || direction == MoveDirection.South;
            var isMovingHorizontal = direction == MoveDirection.East || direction == MoveDirection.West;

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
            case MoveDirection.East: deltaX = 1; break;
            case MoveDirection.West: deltaX = -1; break;
        }

        // Importante: Manter o IsHit original (que deve ser false aqui)
        return Coordinates.Select(c => new Coordinate(c.X + deltaX, c.Y + deltaY, c.IsHit)).ToList();
    }

    public void ConfirmMovement(List<Coordinate> newCoordinates)
    {
        if (newCoordinates.Count != Size)
            throw new InvalidOperationException("Erro crítico: Coordenadas de movimento inválidas.");

        Coordinates = newCoordinates;
    }

    public void UpdateDamage(List<Coordinate> updatedCoordinates)
    {
        if (updatedCoordinates.Count != Size) throw new ArgumentException("Tamanho inválido para atualização de dano.");
        Coordinates = updatedCoordinates;
    }
}