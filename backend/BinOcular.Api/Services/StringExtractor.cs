using System.Text;
using System.Text.RegularExpressions;

namespace BinOcular.Api.Services;

public sealed partial class StringExtractor
{
    public sealed class StringItem
    {
        public long Offset { get; set; }
        public string OffsetHex => $"0x{Offset:X8}";
        public int Length { get; set; }
        public string Encoding { get; set; } = "ASCII";
        public string Value { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Severity { get; set; }
    }

    public static List<StringItem> Extract(byte[] bytes, int minLength = 4, int maxStrings = 25000)
    {
        var list = new List<StringItem>(Math.Min(maxStrings, 2000));
        var sb = new StringBuilder();

        // 1. ASCII string extraction
        var startOffset = 0;
        for (var i = 0; i < bytes.Length && list.Count < maxStrings; i++)
        {
            var b = bytes[i];
            if (b >= 32 && b <= 126) // Printable ASCII
            {
                if (sb.Length == 0) startOffset = i;
                sb.Append((char)b);
            }
            else
            {
                if (sb.Length >= minLength)
                {
                    list.Add(new StringItem
                    {
                        Offset = startOffset,
                        Length = sb.Length,
                        Encoding = "ASCII",
                        Value = sb.ToString()
                    });
                }
                sb.Clear();
            }
        }
        if (sb.Length >= minLength && list.Count < maxStrings)
        {
            list.Add(new StringItem
            {
                Offset = startOffset,
                Length = sb.Length,
                Encoding = "ASCII",
                Value = sb.ToString()
            });
        }

        // 2. UTF-16 LE string extraction
        sb.Clear();
        startOffset = 0;
        for (var i = 0; i + 1 < bytes.Length && list.Count < maxStrings; i += 2)
        {
            var b1 = bytes[i];
            var b2 = bytes[i + 1];

            if (b2 == 0 && b1 >= 32 && b1 <= 126)
            {
                if (sb.Length == 0) startOffset = i;
                sb.Append((char)b1);
            }
            else
            {
                if (sb.Length >= minLength)
                {
                    list.Add(new StringItem
                    {
                        Offset = startOffset,
                        Length = sb.Length,
                        Encoding = "UTF-16",
                        Value = sb.ToString()
                    });
                }
                sb.Clear();
            }
        }
        if (sb.Length >= minLength && list.Count < maxStrings)
        {
            list.Add(new StringItem
            {
                Offset = startOffset,
                Length = sb.Length,
                Encoding = "UTF-16",
                Value = sb.ToString()
            });
        }

        TagIocs(list);
        return list;
    }

    public static void TagIocs(List<StringItem> items)
    {
        foreach (var item in items)
        {
            var trimmed = item.Value.Trim();
            if (trimmed.Length < 4) continue;

            // Network URLs
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
            {
                item.Category = "Network / URL";
                item.Severity = "High";
                continue;
            }

            // IP Addresses
            if (IpRegex().IsMatch(trimmed))
            {
                item.Category = "IPv4 Address";
                item.Severity = "Medium";
                continue;
            }

            // Shell Commands
            if (trimmed.Contains("cmd.exe", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("powershell", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("/bin/sh", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("/bin/bash", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("wscript.exe", StringComparison.OrdinalIgnoreCase))
            {
                item.Category = "Command Execution";
                item.Severity = "High";
                continue;
            }

            // Process Injection APIs
            if (trimmed.Equals("VirtualAlloc", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("VirtualProtect", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("WriteProcessMemory", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("CreateRemoteThread", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("QueueUserAPC", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("ptrace", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("mprotect", StringComparison.OrdinalIgnoreCase))
            {
                item.Category = "Process Injection API";
                item.Severity = "High";
                continue;
            }

            // Defense Evasion APIs
            if (trimmed.Equals("IsDebuggerPresent", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("CheckRemoteDebuggerPresent", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("OutputDebugStringA", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("amsi.dll", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("vmware", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("vbox", StringComparison.OrdinalIgnoreCase))
            {
                item.Category = "Defense Evasion";
                item.Severity = "Medium";
                continue;
            }

            // Persistence & Registry Keys
            if (trimmed.Contains(@"CurrentVersion\Run", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(@"HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(@"HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(@"/etc/crontab", StringComparison.OrdinalIgnoreCase))
            {
                item.Category = "Persistence / Registry";
                item.Severity = "High";
                continue;
            }
        }
    }

    [GeneratedRegex(@"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b")]
    private static partial Regex IpRegex();
}
