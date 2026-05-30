using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using FastEndpoints;
using FluentValidation;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Access.Grant;

internal sealed class GrantAccessValidator : Validator<GrantAccessRequest>
{
    public GrantAccessValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
