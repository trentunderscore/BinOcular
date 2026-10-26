using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BinOcular.Api.Analysis;
using BinOcular.Api.Models;

namespace BinOcular.Api.Tools;

public sealed partial class NetworkEndpointTool(ILogger<NetworkEndpointTool> logger) : IAnalysisTool
{
    public string Name => "Network Endpoint & C2 IOC Extractor";

    public bool IsCompatible(string filePath) => File.Exists(filePath);

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Extracting network endpoints and C2 IOCs for {FilePath}", filePath);

        try
        {
            var data = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var ascii = Encoding.ASCII.GetString(data);

            var endpoints = new List<NetworkEndpointEntry>();

            // 1. URLs
            var urlMatches = UrlRegex().Matches(ascii);
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in urlMatches)
            {
                var url = match.Value.TrimEnd('.', ',', ')', ']', '}', '"', '\'');
                if (url.Length >= 10 && seenUrls.Add(url) && !url.Contains("schema.org") && !url.Contains("w3.org") && !url.Contains("schemas.microsoft.com"))
                {
                    var isSuspicious = url.Contains(".onion") || url.Contains("duckdns") || url.Contains("ngrok") ||
                                       url.Contains("pastebin") || url.Contains("discord.com/api/webhooks") ||
                                       url.Contains("telegram.org/bot");

                    endpoints.Add(new NetworkEndpointEntry
                    {
                        Type = "URL / URI",
                        Value = url,
                        Context = isSuspicious ? "Known Staging / C2 Exfiltration Vector" : "External Web Endpoint",
                        Risk = isSuspicious ? "Critical" : "Medium"
                    });
                }
            }

            // 2. IPv4 Addresses
            var ipMatches = Ipv4Regex().Matches(ascii);
            var seenIps = new HashSet<string>();
            foreach (Match match in ipMatches)
            {
                var ip = match.Value;
                if (!seenIps.Add(ip)) continue;

                // Filter false positives / common non-routable strings
                if (ip == "0.0.0.0" || ip == "255.255.255.255" || ip.StartsWith("127.")) continue;

                var isPrivate = ip.StartsWith("10.") || ip.StartsWith("192.168.") ||
                                (ip.StartsWith("172.") && int.TryParse(ip.Split('.')[1], out var octet2) && octet2 is >= 16 and <= 31);

                endpoints.Add(new NetworkEndpointEntry
                {
                    Type = isPrivate ? "Private IPv4" : "Public IPv4",
                    Value = ip,
                    Context = isPrivate ? "Local Area / Internal RFC-1918 Host" : "Routable External Destination",
                    Risk = isPrivate ? "Low" : "High"
                });
            }

            // 3. User-Agent Strings
            var uaMatches = UserAgentRegex().Matches(ascii);
            var seenUas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in uaMatches)
            {
                var ua = match.Value;
                if (seenUas.Add(ua) && ua.Length > 12)
                {
                    endpoints.Add(new NetworkEndpointEntry
                    {
                        Type = "HTTP User-Agent",
                        Value = ua,
                        Context = "Custom HTTP Client Header (Potential C2 Beacon Beaconing Profile)",
                        Risk = "Medium"
                    });
                }
            }

            // 4. DNS / Domain Names (Suspicious TLDs or Dynamic DNS)
            string[] suspiciousDomains = ["duckdns.org", "no-ip.biz", "hopto.org", "ngrok-free.app", "serveo.net", "pagekite.me", "temp.sh", "transfer.sh"];
            foreach (var domain in suspiciousDomains)
            {
                if (ascii.Contains(domain, StringComparison.OrdinalIgnoreCase))
                {
                    endpoints.Add(new NetworkEndpointEntry
                    {
                        Type = "Dynamic DNS / Tunnel",
                        Value = domain,
                        Context = "Dynamic DNS or Reverse Tunneling Service commonly used for C2 redirection",
                        Risk = "Critical"
                    });
                }
            }

            var summary = endpoints.Count switch
            {
                0 => "No network endpoints, URLs, or external communication indicators detected.",
                1 => "1 network communication artifact identified.",
                _ => $"{endpoints.Count} network endpoints and communication indicators identified."
            };

            var result = new
            {
                totalEndpoints = endpoints.Count,
                summary,
                endpoints = endpoints.Take(100) // cap at 100 for display
            };

            return new ToolResult
            {
                Tool = Name,
                ExitCode = 0,
                StandardOutput = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }),
                StandardError = string.Empty
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NetworkEndpointTool failed for {FilePath}", filePath);
            return new ToolResult
            {
                Tool = Name,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message
            };
        }
    }

    [GeneratedRegex(@"https?:\/\/[a-zA-Z0-9\-\._~:\/\?#\[\]@!$&'\(\)\*\+,;=%]+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b", RegexOptions.Compiled)]
    private static partial Regex Ipv4Regex();

    [GeneratedRegex(@"Mozilla\/[0-9]\.[0-9][^\r\n]{10,80}", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UserAgentRegex();

    private sealed class NetworkEndpointEntry
    {
        public required string Type { get; set; }
        public required string Value { get; set; }
        public required string Context { get; set; }
        public required string Risk { get; set; }
    }
}
