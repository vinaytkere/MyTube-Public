using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyTube.Backend.Application.Iptv;

namespace MyTube.Backend.Infrastructure.Iptv;

public class SsrfValidator : ISsrfValidator
{
    private readonly ILogger<SsrfValidator> _logger;

    public SsrfValidator(ILogger<SsrfValidator> logger)
    {
        _logger = logger;
    }

    public async Task<SsrfValidationResult> ValidateUrlAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return SsrfValidationResult.Blocked("URL is null or empty.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return SsrfValidationResult.Blocked("Malformed URL.");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return SsrfValidationResult.Blocked($"Invalid URI scheme '{uri.Scheme}'. Only HTTP and HTTPS are permitted.");
        }

        var host = uri.DnsSafeHost;
        if (string.IsNullOrWhiteSpace(host))
        {
            return SsrfValidationResult.Blocked("Host is missing.");
        }

        // Host name string checks
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("metadata.google.internal", StringComparison.OrdinalIgnoreCase))
        {
            return SsrfValidationResult.Blocked($"Forbidden hostname '{host}'.");
        }

        try
        {
            IPAddress[] addresses;
            if (IPAddress.TryParse(host, out var directIp))
            {
                addresses = [directIp];
            }
            else
            {
                addresses = await Dns.GetHostAddressesAsync(host, ct);
            }

            if (addresses.Length == 0)
            {
                return SsrfValidationResult.Blocked("Hostname could not be resolved.");
            }

            foreach (var address in addresses)
            {
                var checkIp = address;
                if (checkIp.IsIPv4MappedToIPv6)
                {
                    checkIp = checkIp.MapToIPv4();
                }

                if (IsPrivateOrForbidden(checkIp))
                {
                    _logger.LogWarning("SSRF attempt blocked: host '{Host}' resolved to private/restricted IP '{Ip}'.", host, address);
                    return SsrfValidationResult.Blocked($"Forbidden target IP '{address}'.");
                }
            }

            return SsrfValidationResult.Success();
        }
        catch (SocketException ex)
        {
            return SsrfValidationResult.Blocked($"DNS resolution failure: {ex.Message}");
        }
        catch (Exception ex)
        {
            return SsrfValidationResult.Blocked($"Validation error: {ex.Message}");
        }
    }

    private static bool IsPrivateOrForbidden(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();

            // 0.0.0.0/8
            if (bytes[0] == 0) return true;

            // 10.0.0.0/8
            if (bytes[0] == 10) return true;

            // 127.0.0.0/8
            if (bytes[0] == 127) return true;

            // 169.254.0.0/16 (Link-local & Cloud Metadata)
            if (bytes[0] == 169 && bytes[1] == 254) return true;

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;

            // 100.64.0.0/10 (CGNAT)
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) return true;

            // 198.18.0.0/15 (Benchmarking)
            if (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19)) return true;

            // Broadcast
            if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255) return true;
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;

            var bytes = ip.GetAddressBytes();
            // Unique local addresses fc00::/7 (0xfc or 0xfd)
            if ((bytes[0] & 0xfe) == 0xfc) return true;
        }

        return false;
    }
}

