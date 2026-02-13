using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Domain.Enums;
using FluentValidation;

namespace BatalhaNaval.Application.Validators;

public class PlaceShipsValidator : AbstractValidator<PlaceShipsInput>
{
    public PlaceShipsValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty();

        RuleFor(x => x.Ships)
            .NotEmpty().WithMessage("A lista de navios não pode estar vazia.")
            .Must(HaveCorrectFleetComposition)
            .WithMessage(
                "A frota deve conter: 2 Porta-Aviões (6), 2 Navios de guerra (4), 1 Encouraçado (3), e 1 Submarino (1).");

        RuleForEach(x => x.Ships).SetValidator(new ShipPlacementValidator());
    }

    private bool HaveCorrectFleetComposition(List<ShipPlacementDto> ships)
    {
        if (ships == null || ships.Count != 6) return false;
        var counts = ships.GroupBy(s => s.Size).ToDictionary(g => g.Key, g => g.Count());
        return counts.GetValueOrDefault(6) == 2 &&
               counts.GetValueOrDefault(4) == 2 &&
               counts.GetValueOrDefault(3) == 1 &&
               counts.GetValueOrDefault(1) == 1;
    }
}

public class ShipPlacementValidator : AbstractValidator<ShipPlacementDto>
{
    public ShipPlacementValidator()
    {
        RuleFor(s => s.Name).NotEmpty();
        RuleFor(s => s.Size).InclusiveBetween(1, 6);
        RuleFor(s => s.StartX).InclusiveBetween(0, 9);
        RuleFor(s => s.StartY).InclusiveBetween(0, 9);
        RuleFor(s => s.Orientation).IsInEnum();

        RuleFor(s => s).Must(FitInBoard).WithMessage("O navio ultrapassa os limites do tabuleiro.");
    }

    private bool FitInBoard(ShipPlacementDto ship)
    {
        var endX = ship.Orientation == ShipOrientation.Horizontal ? ship.StartX + ship.Size - 1 : ship.StartX;
        var endY = ship.Orientation == ShipOrientation.Vertical ? ship.StartY + ship.Size - 1 : ship.StartY;
        return endX < 10 && endY < 10;
    }
}