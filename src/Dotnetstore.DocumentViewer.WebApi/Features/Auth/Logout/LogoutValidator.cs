using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using FastEndpoints;
using FluentValidation;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Auth.Logout;

internal sealed class LogoutValidator : Validator<LogoutRequest>
{
    public LogoutValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
