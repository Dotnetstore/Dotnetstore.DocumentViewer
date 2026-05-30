using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using FastEndpoints;
using FluentValidation;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Users.Update;

internal sealed class UpdateUserValidator : Validator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Roles).NotNull().Must(rs => rs.Count > 0 && rs.All(r => RoleNames.All.Contains(r)))
            .WithMessage($"Roles must be non-empty and only contain: {string.Join(", ", RoleNames.All)}.");
    }
}
