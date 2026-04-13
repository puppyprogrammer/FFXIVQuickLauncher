using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Serilog;

namespace XIVLauncher.Common.Util;

/// <summary>
/// TLS certificate pinning for the FFXIVPlugins API domain.
/// Validates that the server's SPKI hash matches one of the pinned values,
/// preventing MITM attacks even if a CA is compromised.
/// </summary>
public static class CertificatePinning
{
    /// <summary>
    /// SPKI SHA-256 hashes (base64) we trust for our API domain.
    /// Primary = leaf key, backup = Let's Encrypt intermediate.
    /// </summary>
    private static readonly HashSet<string> PinnedSpkiHashes = new()
    {
        "kBfonzK8yg2GwgZSBzKhOTMvHsYmD15HFTlSDE3ae2k=", // leaf key
        "iFvwVyJSxnQdyaUvUERIf+8qk7gRze3612JMwoO3zdU=", // Let's Encrypt R11 intermediate (backup)
    };

    /// <summary>
    /// The hostname to enforce certificate pinning on.
    /// </summary>
    private const string PinnedHostname = "ffxivplugins.commslink.net";

    /// <summary>
    /// TLS certificate validation callback. Enforces SPKI pinning for the API domain;
    /// all other hosts use standard platform validation.
    /// </summary>
    public static bool ValidateCallback(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        // For non-pinned hosts, use default validation only.
        var isPinnedHost = false;
        if (sender is SslStream ssl)
            isPinnedHost = string.Equals(ssl.TargetHostName, PinnedHostname, StringComparison.OrdinalIgnoreCase);
        else if (sender is HttpRequestMessage req)
            isPinnedHost = string.Equals(req.RequestUri?.Host, PinnedHostname, StringComparison.OrdinalIgnoreCase);

        if (!isPinnedHost)
            return sslPolicyErrors == SslPolicyErrors.None;

        if (sslPolicyErrors != SslPolicyErrors.None)
        {
            Log.Warning("[CertPin] TLS validation failed for {Host}: {Errors}", PinnedHostname, sslPolicyErrors);
            return false;
        }

        if (chain == null || certificate == null)
            return false;

        // Check every certificate in the chain for a matching SPKI pin.
        foreach (var element in chain.ChainElements)
        {
            using var cert = element.Certificate;
            var spkiHash = Convert.ToBase64String(SHA256.HashData(cert.PublicKey.ExportSubjectPublicKeyInfo()));
            if (PinnedSpkiHashes.Contains(spkiHash))
                return true;
        }

        // Also check the leaf directly.
        using var leaf = new X509Certificate2(certificate);
        var leafHash = Convert.ToBase64String(SHA256.HashData(leaf.PublicKey.ExportSubjectPublicKeyInfo()));
        if (PinnedSpkiHashes.Contains(leafHash))
            return true;

        Log.Error(
            "[CertPin] Pin FAILED for {Host}. Leaf SPKI: {LeafHash}. Possible MITM or cert rotation.",
            PinnedHostname,
            leafHash);
        return false;
    }

    /// <summary>
    /// Returns an <see cref="SslClientAuthenticationOptions"/> with pinning enabled.
    /// Use this when constructing a <see cref="System.Net.Http.SocketsHttpHandler"/>.
    /// </summary>
    public static SslClientAuthenticationOptions CreateSslOptions() => new()
    {
        RemoteCertificateValidationCallback = ValidateCallback,
    };
}
