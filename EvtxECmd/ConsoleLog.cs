using System;
using System.IO;
using System.Reflection;

namespace EvtxECmd;

/// <summary>--co (Issues #259): the console messages are also saved to &lt;yyyyMMddHHmmss&gt;_EvtxECmd_&lt;version&gt;_ConsoleLog.txt in the output directory</summary>
internal static class ConsoleLog
{
    public const string Template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>The log file in the first output directory given (the time is UTC, as in the output file names)</summary>
    /// <returns>null when no output directory was given or the path is not valid (the tool reports that itself)</returns>
    public static string GetPath(DateTimeOffset utcNow, params string[] outputDirectories)
    {
        foreach (var dir in outputDirectories)
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                continue;
            }

            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);
                return Path.Combine(Path.GetFullPath(dir), $"{utcNow:yyyyMMddHHmmss}_EvtxECmd_{version}_ConsoleLog.txt");
            }
            catch (Exception)
            {
                return null;
            }
        }

        return null;
    }
}
