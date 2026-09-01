using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace paperframe_server.Services;

/// <summary>Where the durable copy of the check-in log is written.</summary>
public record LogFilePointer(string FilePath);

/// <summary>
/// Mirrors check-in entries to disk beside the configuration.
///
/// The in-memory log holds the last hundred entries and dies with the process, which is
/// the opposite of what a device problem needs: those are diagnosed days later, across a
/// restart, from lines a Kindle delivered whenever it next managed to reach the server.
///
/// Failures here are swallowed on purpose. Losing the durable copy is worth noticing, but
/// never worth failing a device's check-in over — the device has nowhere to go with the
/// error and would stop rendering.
/// </summary>
public sealed class PaperframeLogFile
{
    private const long MaxBytes = 4 * 1024 * 1024;

    private readonly string _path;
    private readonly object _lock = new();

    public PaperframeLogFile(LogFilePointer pointer)
    {
        _path = pointer.FilePath;
    }

    public void Append(PaperframeLogEntry entry) => Append([entry]);

    /// <summary>
    /// Writes a batch as one operation. A device can deliver dozens of lines in a single
    /// request, and appending them one at a time meant a directory check, a stat and a file
    /// open apiece with the whole log service held.
    /// </summary>
    public void Append(IReadOnlyList<PaperframeLogEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        lock (_lock)
        {
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                Trim();

                File.AppendAllLines(_path, entries.Select(Format));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not write the Paperframe log file: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Tab separated so the columns survive a message containing anything, and so the file
    /// stays greppable — this gets read in a terminal far more often than by a program.
    /// </summary>
    private static string Format(PaperframeLogEntry entry) => string.Join('\t',
        entry.Timestamp.ToString("s", CultureInfo.InvariantCulture),
        entry.DeviceId,
        entry.Status,
        entry.Service,
        entry.ConfigId,
        entry.ScriptVersion,
        entry.Battery?.ToString(CultureInfo.InvariantCulture) ?? "-",
        Flatten(entry.Message));

    /// <summary>
    /// Anything that would break the column layout becomes a space. Messages carry device
    /// stderr, so a tab in one of them would silently shift every column after it.
    /// </summary>
    private static string Flatten(string message) =>
        message.ReplaceLineEndings(" ").Replace('\t', ' ');

    /// <summary>
    /// Keeps the newest half once the file outgrows its ceiling. Same shape as the trim on
    /// the device, for the same reason: the tail is the part anyone reads, and one file is
    /// simpler to hand to someone than a numbered set.
    /// </summary>
    private void Trim()
    {
        var file = new FileInfo(_path);
        if (!file.Exists || file.Length <= MaxBytes)
        {
            return;
        }

        // Through a temporary file: rewriting in place would leave the log this class exists
        // to protect truncated if the process died midway.
        var lines = File.ReadAllLines(_path);
        var trimmed = _path + ".tmp";

        File.WriteAllLines(trimmed, lines[(lines.Length / 2)..]);
        File.Move(trimmed, _path, overwrite: true);
    }
}
