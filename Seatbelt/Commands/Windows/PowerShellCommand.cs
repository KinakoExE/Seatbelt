﻿using Microsoft.Win32;
using Seatbelt.Output.Formatters;
using Seatbelt.Output.TextWriters;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Seatbelt.Commands.Windows
{
    // TODO: Check for the presence of the PSReadline log file
    internal class PowerShellCommand : CommandBase
    {
        public override string Command => "PowerShell";
        public override string Description => "PowerShell versions and security settings";
        public override CommandGroup[] Group => new[] { CommandGroup.System, CommandGroup.Remote };
        public override bool SupportRemote => true;
        public Runtime ThisRunTime;

        public PowerShellCommand(Runtime runtime) : base(runtime)
        {
            ThisRunTime = runtime;
        }

        private IEnumerable<string> GetWindowsPowerShellVersions()
        {
            var versions = new List<string>();
            var PowerShellVersion2 = ThisRunTime.GetStringValue(RegistryHive.LocalMachine, "SOFTWARE\\Microsoft\\PowerShell\\1\\PowerShellEngine", "PowerShellVersion");

            if (PowerShellVersion2 != null)
            {
                versions.Add(PowerShellVersion2);
            }

            var PowerShellVersion4Plus = ThisRunTime.GetStringValue(RegistryHive.LocalMachine, "SOFTWARE\\Microsoft\\PowerShell\\3\\PowerShellEngine", "PowerShellVersion");
            if (PowerShellVersion4Plus != null)
            {
                versions.Add(PowerShellVersion4Plus);
            }

            return versions;
        }

        private IEnumerable<string> GetPowerShellCoreVersions()
        {
            var versions = new List<string>();

            var keys = ThisRunTime.GetSubkeyNames(RegistryHive.LocalMachine,
                           @"SOFTWARE\Microsoft\PowerShellCore\InstalledVersions\") ?? new string[] { };


            foreach (var key in keys)
            {
                var version = ThisRunTime.GetStringValue(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\PowerShellCore\InstalledVersions\" + key, "SemanticVersion");
                if (version != null)
                {
                    versions.Add(version);
                }
            }

            return versions;
        }


        public override IEnumerable<CommandDTOBase?> Execute(string[] args)
        {

            var installedVersions = new List<string>();
            installedVersions.AddRange(GetWindowsPowerShellVersions());
            installedVersions.AddRange(GetPowerShellCoreVersions());


            var transcriptionLogging = ThisRunTime.GetStringValue(RegistryHive.LocalMachine, "SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\Transcription", "EnableTranscripting") == "1";
            var transcriptionInvocationLogging = ThisRunTime.GetStringValue(RegistryHive.LocalMachine, "SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\Transcription", "EnableInvocationHeader") == "1";
            var transcriptionDirectory = ThisRunTime.GetStringValue(RegistryHive.LocalMachine, "SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\Transcription", "OutputDirectory");
            var moduleLogging = ThisRunTime.GetStringValue(RegistryHive.LocalMachine, "SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\ModuleLogging", "EnableModuleLogging") == "1";
            var moduleNames = ThisRunTime.GetValues(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\PowerShell\ModuleLogging\ModuleNames")?.Keys.ToArray();
            var scriptBlockLogging = ThisRunTime.GetStringValue(RegistryHive.LocalMachine, "SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\ScriptBlockLogging", "EnableScriptBlockLogging") == "1";
            var scriptBlockInvocationLogging = ThisRunTime.GetStringValue(RegistryHive.LocalMachine,
                                                   "SOFTWARE\\Policies\\Microsoft\\Windows\\PowerShell\\ScriptBlockLogging",
                                                   "EnableScriptBlockInvocationLogging") == "1";
            var osSupportsAmsi = Environment.OSVersion.Version.Major >= 10;

            yield return new PowerShellDTO(
                installedVersions.ToArray(),
                transcriptionLogging,
                transcriptionInvocationLogging,
                transcriptionDirectory,
                moduleLogging,
                moduleNames,
                scriptBlockLogging,
                scriptBlockInvocationLogging,
                osSupportsAmsi
            );
        }
    }

    class PowerShellDTO : CommandDTOBase
    {
        public PowerShellDTO(string[] installedVersions, bool transcriptionLogging, bool transcriptionInvocationLogging, string? transcriptionDirectory, bool moduleLogging, string[]? moduleNames, bool scriptBlockLogging, bool scriptBlockInvocationLogging, bool osSupportsAmsi)
        {
            InstalledVersions = installedVersions;
            TranscriptionLogging = transcriptionLogging;
            TranscriptionInvocationLogging = transcriptionInvocationLogging;
            TranscriptionDirectory = transcriptionDirectory;
            ModuleLogging = moduleLogging;
            ModuleNames = moduleNames;
            ScriptBlockLogging = scriptBlockLogging;
            ScriptBlockInvocationLogging = scriptBlockInvocationLogging;
            OsSupportsAmsi = osSupportsAmsi;
        }
        public string[] InstalledVersions { get; }
        public bool? TranscriptionLogging { get; }
        public bool? TranscriptionInvocationLogging { get; }
        public string? TranscriptionDirectory { get; }
        public bool ModuleLogging { get; }
        public string[]? ModuleNames { get; }
        public bool ScriptBlockLogging { get; }
        public bool? ScriptBlockInvocationLogging { get; }
        public bool OsSupportsAmsi { get; }
    }

    [CommandOutputType(typeof(PowerShellDTO))]
    internal class PowerShellTextFormatter : TextFormatterBase
    {
        public PowerShellTextFormatter(ITextWriter writer) : base(writer)
        {
        }

        public override void FormatResult(CommandBase? command, CommandDTOBase result, bool filterResults)
        {
            var dto = (PowerShellDTO)result;
            var lowestVersion = dto.InstalledVersions.Min(v => (new Version(v)));
            var highestVersion = dto.InstalledVersions.Max(v => (new Version(v)));

            WriteLine("  Installed PowerShell Versions");
            foreach (var v in dto.InstalledVersions)
            {
                WriteLine("      " + v);
            }

            WriteLine("\n  Transcription Logging Settings");
            WriteLine("      Enabled            : " + dto.TranscriptionLogging);
            WriteLine("      Invocation Logging : " + dto.TranscriptionInvocationLogging);
            WriteLine("      Log Directory      : " + dto.TranscriptionDirectory);

            WriteLine("\n  Module Logging Settings");
            WriteLine("      Enabled             : " + dto.ModuleLogging);
            WriteLine("      Logged Module Names :");

            if (dto.ModuleNames != null)
            {
                foreach (var m in dto.ModuleNames)
                {
                    WriteLine("          " + m);
                }
            }

            if (dto.ModuleLogging)
            {
                if (lowestVersion.Major < 3)
                {
                    WriteLine("      [!] You can do a PowerShell version downgrade to bypass the logging.");
                }

                if (highestVersion.Major < 3)
                {
                    WriteLine("      [!] Module logging is configured. Logging will not occur, however, because it requires PSv3.");
                }
            }


            WriteLine("\n  Script Block Logging Settings");
            WriteLine("      Enabled            : " + dto.ScriptBlockLogging);
            WriteLine("      Invocation Logging : " + dto.ScriptBlockInvocationLogging);
            if (dto.ScriptBlockLogging)
            {
                if (highestVersion.Major < 5)
                {
                    WriteLine("      [!] Script block logging is configured. Logging will not occur, however, because it requires PSv5.");
                }

                if (lowestVersion.Major < 5)
                {
                    WriteLine("      [!] You can do a PowerShell version downgrade to bypass the logging.");
                }
            }

            WriteLine("\n  Anti-Malware Scan Interface (AMSI)");
            WriteLine("      OS Supports AMSI: " + dto.OsSupportsAmsi);
            if (dto.OsSupportsAmsi && lowestVersion.Major < 3)
            {
                WriteLine("      [!] You can do a PowerShell version downgrade to bypass AMSI.");
            }
        }
    }
}
