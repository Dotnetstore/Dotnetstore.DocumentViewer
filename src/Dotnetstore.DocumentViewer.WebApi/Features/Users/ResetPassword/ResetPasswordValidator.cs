using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;
using FastEndpoints;
using FluentValidation;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Users.ResetPassword;

internal sealed class ResetPasswordValidator : Validator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}
