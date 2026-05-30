using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using FastEndpoints;
using FluentValidation;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Auth.ChangePassword;

internal sealed class ChangePasswordValidator : Validator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}
