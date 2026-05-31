using System.Net;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using FastEndpoints;
using FluentValidation;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Access.AllowedIps;

internal sealed class AddAllowedIpValidator : Validator<AddAllowedIpRequest>
{
    public AddAllowedIpValidator()
    {
        RuleFor(x => x.Cidr)
            .NotEmpty()
            .MaximumLength(64)
            .Must(NormalizeCidr.IsParseable)
            .WithMessage("Cidr must be a valid IP address or CIDR (e.g. '10.0.0.0/8', '203.0.113.5').");

        RuleFor(x => x.Description).MaximumLength(200);
    }
}

/// <summary>
/// Accepts a bare IP (192.0.2.5, 2001:db8::1) or a CIDR (10.0.0.0/8). A bare IP is
/// normalised to a single-host CIDR so storage / lookup is uniform.
/// </summary>
internal static class NormalizeCidr
{
    public static bool IsParseable(string? input) => TryNormalize(input, out _);

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var trimmed = input.Trim();

        if (IPNetwork.TryParse(trimmed, out var network))
        {
            normalized = network.ToString();
            return true;
        }

        if (IPAddress.TryParse(trimmed, out var address))
        {
            var prefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;
            if (IPNetwork.TryParse($"{address}/{prefix}", out var hostNetwork))
            {
                normalized = hostNetwork.ToString();
                return true;
            }
        }

        return false;
    }
}
