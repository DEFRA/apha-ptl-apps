using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace PTL.Common.Security;

/// <summary>The per-request nonce that lets the page's own inline scripts run under the Content-Security-Policy.</summary>
public static class CspNonce
{
    private static readonly object ItemKey = new();

    public static string Create(HttpContext context)
    {
        // Hex, so the value is written into nonce="..." attributes unchanged by HTML encoding.
        var nonce = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        context.Items[ItemKey] = nonce;
        return nonce;
    }

    public static string? Get(HttpContext? context) => context?.Items[ItemKey] as string;
}
