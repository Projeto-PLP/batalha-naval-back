namespace BatalhaNaval.Domain.ValueObjects;

public readonly record struct Coordinate(int X, int Y, bool IsHit = false)
{
    public bool IsWithinBounds(int gridSize) => 
        X >= 0 && X < gridSize && Y >= 0 && Y < gridSize;
}