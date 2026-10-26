using System.Text;
using System.Text.Json;
using BinOcular.Api.Analysis;
using BinOcular.Api.Models;

namespace BinOcular.Api.Tools;

public sealed class MitreAttackMapperTool(ILogger<MitreAttackMapperTool> logger) : IAnalysisTool
{
    public string Name => "MITRE ATT&CK Framework Matrix Mapper";

    public bool IsCompatible(string filePath) => File.Exists(filePath);

    public async Task<ToolResult> AnalyzeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Mapping MITRE ATT&CK techniques for {FilePath}", filePath);

        try
        {
            var data = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var text = Encoding.ASCII.GetString(data);

            var techniques = new List<MitreTechnique>();

            // Helper to check multiple triggers
            void CheckTechnique(
                string tactic,
                string techniqueId,
                string techniqueName,
                string[] keywords,
                string description)
            {
                var matched = keywords.Where(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matched.Count > 0)
                {
                    techniques.Add(new MitreTechnique
                    {
                        Tactic = tactic,
                        TechniqueId = techniqueId,
                        TechniqueName = techniqueName,
                        MatchedEvidence = string.Join(", ", matched.Take(4)),
                        Description = description
                    });
                }
            }

            // Execution
            CheckTechnique("Execution", "T1059", "Command and Scripting Interpreter",
                ["cmd.exe", "powershell", "/bin/sh", "/bin/bash", "wscript.exe", "cscript.exe"],
                "Executes system commands through built-in shell or scripting engines.");

            CheckTechnique("Execution", "T1106", "Native API Execution",
                ["CreateProcess", "ShellExecute", "WinExec", "ZwCreateSection"],
                "Directly invokes OS native APIs to instantiate processes.");

            // Persistence
            CheckTechnique("Persistence", "T1547.001", "Registry Run Keys / Startup Folder",
                [@"CurrentVersion\Run", @"CurrentVersion\RunOnce", @"Software\Microsoft\Windows\CurrentVersion\Run"],
                "Adds persistence by registering executables in autostart registry keys.");

            CheckTechnique("Persistence", "T1543.003", "Windows Service Creation",
                ["CreateService", "OpenSCManager", "StartServiceCtrlDispatcher"],
                "Installs or modifies Windows system services for persistent background execution.");

            CheckTechnique("Persistence", "T1053", "Scheduled Task / Job",
                ["schtasks", "TaskScheduler", "ITaskService", "RegisterTaskDefinition"],
                "Schedules periodic task execution using the Windows Task Scheduler.");

            // Privilege Escalation / Injection
            CheckTechnique("Privilege Escalation", "T1055.001", "Process Injection: Dynamic-Link Library Injection",
                ["VirtualAllocEx", "WriteProcessMemory", "CreateRemoteThread", "LoadLibraryA"],
                "Injects foreign code or libraries into the address space of external processes.");

            CheckTechnique("Privilege Escalation", "T1055.012", "Process Hollowing",
                ["NtUnmapViewOfSection", "ZwUnmapViewOfSection", "SetThreadContext", "ResumeThread"],
                "Replaces memory of a legitimate process container with hostile payload.");

            // Defense Evasion
            CheckTechnique("Defense Evasion", "T1562.001", "Impair Defenses: Disable or Modify Tools",
                ["amsi.dll", "AmsiScanBuffer", "DisableAntiSpyware", "DisableRealtimeMonitoring", "MpPreference"],
                "Attempts to bypass AMSI, tamper with Windows Defender, or suppress logging.");

            CheckTechnique("Defense Evasion", "T1497.001", "Virtualization / Sandbox Evasion",
                ["IsDebuggerPresent", "CheckRemoteDebuggerPresent", "GetTickCount", "OutputDebugString", "VBox", "VMware"],
                "Probes environment for hypervisors, sandboxes, or attached debuggers.");

            CheckTechnique("Defense Evasion", "T1070", "Indicator Removal on Host",
                ["wevtutil", "ClearEventLog", "fsutil usn deletejournal"],
                "Deletes or clears event logs and journals to prevent forensic analysis.");

            // Credential Access
            CheckTechnique("Credential Access", "T1003.001", "OS Credential Dumping: LSASS Memory",
                ["lsass.exe", "MiniDumpWriteDump", "SeDebugPrivilege", "SAM"],
                "Accesses system credential storage or process memory to extract tokens/passwords.");

            CheckTechnique("Credential Access", "T1056.001", "Input Capture: Keylogging",
                ["SetWindowsHookEx", "GetAsyncKeyState", "GetKeyboardState"],
                "Monitors user keystrokes to harvest passwords and sensitive input.");

            CheckTechnique("Credential Access", "T1555", "Credentials from Password Stores",
                ["Login Data", "signons.sqlite", "VaultCli", "CryptUnprotectData", "cookies.sqlite"],
                "Harvests stored credentials and session tokens from web browsers and vault stores.");

            // Discovery
            CheckTechnique("Discovery", "T1082", "System Information Discovery",
                ["GetComputerName", "GetSystemInfo", "GlobalMemoryStatus", "GetVersionEx"],
                "Gathers host OS version, hardware specifications, and system identity.");

            CheckTechnique("Discovery", "T1057", "Process Discovery",
                ["CreateToolhelp32Snapshot", "Process32First", "Process32Next", "EnumProcesses"],
                "Enumerates active running processes on the host.");

            CheckTechnique("Discovery", "T1083", "File and Directory Discovery",
                ["FindFirstFile", "FindNextFile", "GetLogicalDrives", "GetDriveType"],
                "Scans filesystems and drives to locate user data or targets.");

            // Collection
            CheckTechnique("Collection", "T1113", "Screen Capture",
                ["BitBlt", "GetDC", "CreateCompatibleDC", "GetDesktopWindow"],
                "Takes screenshots or visual captures of the user desktop.");

            CheckTechnique("Collection", "T1115", "Clipboard Data",
                ["OpenClipboard", "GetClipboardData", "CloseClipboard"],
                "Monitors or extracts content copied to the system clipboard.");

            // Command and Control
            CheckTechnique("Command and Control", "T1071.001", "Application Layer Protocol: Web Protocols",
                ["InternetOpen", "HttpOpenRequest", "WinHttpOpen", "WinHttpSendRequest", "curl"],
                "Communicates over standard HTTP/HTTPS protocols for C2 coordination.");

            CheckTechnique("Command and Control", "T1573", "Encrypted Channel",
                ["CryptEncrypt", "BCryptEncrypt", "AES", "ChaCha20", "SSL"],
                "Encrypts outbound C2 communication to evade network inspection.");

            // Exfiltration
            CheckTechnique("Exfiltration", "T1567.002", "Exfiltration Over Web Service",
                ["discord.com/api/webhooks", "api.telegram.org", "pastebin.com"],
                "Exfiltrates stolen credentials and system data using public third-party web services.");

            var summary = techniques.Count switch
            {
                0 => "No standard MITRE ATT&CK tactical techniques recognized.",
                1 => "1 MITRE ATT&CK technique mapped.",
                _ => $"{techniques.Count} MITRE ATT&CK techniques mapped across multiple attack tactics."
            };

            var result = new
            {
                totalTechniques = techniques.Count,
                summary,
                techniques
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
            logger.LogError(ex, "MitreAttackMapperTool failed for {FilePath}", filePath);
            return new ToolResult
            {
                Tool = Name,
                ExitCode = -1,
                StandardOutput = string.Empty,
                StandardError = ex.Message
            };
        }
    }

    private sealed class MitreTechnique
    {
        public required string Tactic { get; set; }
        public required string TechniqueId { get; set; }
        public required string TechniqueName { get; set; }
        public required string MatchedEvidence { get; set; }
        public required string Description { get; set; }
    }
}
