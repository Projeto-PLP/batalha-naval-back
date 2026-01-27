using BatalhaNaval.Domain.ValueObjects;
using BatalhaNaval.Domain.Entities;

namespace BatalhaNaval.Domain.Interfaces;

public interface IAiStrategy
{
    Coordinate ChooseTarget(Board enemyBoard);
}