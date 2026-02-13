using BatalhaNaval.Application.DTOs;
using FluentValidation;

namespace BatalhaNaval.Application.Validators;

public class ShootInputValidator : AbstractValidator<ShootInput>
{
    public ShootInputValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty();

        RuleFor(x => x.X)
            .InclusiveBetween(0, 9).WithMessage("A coordenada X deve estar entre 0 e 9.");

        RuleFor(x => x.Y)
            .InclusiveBetween(0, 9).WithMessage("A coordenada Y deve estar entre 0 e 9.");
    }
}