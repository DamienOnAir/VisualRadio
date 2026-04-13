using VRA.AudioManager.Audio;
using VRA.AudioManager.Config;
using VRA.AudioManager.Gpio;

namespace VRA.AudioManager.Display;

public record DeviceInfo(int Index, string Name, string WindowsId);

public class VuMeterRenderer
{
    private const int BarWidth = 32;
    private const float MinDb  = -60f;
    private const float MaxDb  =   0f;

    private readonly List<ChannelConfig> _channels;
    private readonly LevelMonitor _monitor;
    private readonly IReadOnlyList<DeviceInfo> _allDevices;
    private readonly DhdDriver? _dhd;

    // Clé = DeviceName (config) → DeviceInfo résolu
    private readonly Dictionary<string, DeviceInfo> _resolved;
    private int _renderTop;
    private int _debugLineCount;

    public VuMeterRenderer(
        List<ChannelConfig> channels,
        LevelMonitor monitor,
        IReadOnlyList<DeviceInfo> allDevices,
        Dictionary<string, DeviceInfo> resolved,
        DhdDriver? dhd = null)
    {
        _channels   = channels;
        _monitor    = monitor;
        _allDevices = allDevices;
        _resolved   = resolved;
        _dhd        = dhd;
    }

    public void PrintHeader()
    {
        Console.Clear();
        Console.CursorVisible = false;
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          VRA Audio Manager — VU Meters                  ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine("  Ctrl+C pour quitter");
        Console.WriteLine();

        PrintDeviceList();

        Console.WriteLine(new string('─', 62));
        _renderTop = Console.CursorTop;

        // Mono   : nom + barre M + sous-titre = 3 lignes
        // Stéréo : nom + barre L + barre R + sous-titre = 4 lignes
        int vuLines = _channels.Sum(c => c.ChannelIndex.Length == 1 ? 3 : 4);

        // Section debug : limiter au nombre de lignes disponibles dans la fenêtre
        int windowAvail = Console.WindowHeight - _renderTop - vuLines - 2; // -2 : séparateur bas + marge
        _debugLineCount = _dhd is not null
            ? Math.Max(0, Math.Min(1 + DhdDriver.MaxRecentFrames, windowAvail))
            : 0;

        int lines = vuLines + 1 + _debugLineCount;
        for (int i = 0; i < lines; i++) Console.WriteLine();
    }

    private void PrintDeviceList()
    {
        var activeWindowsIds = _resolved.Values.Select(d => d.WindowsId).ToHashSet();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Périphériques d'entrée disponibles :");
        Console.WriteLine();

        var groups = _allDevices.GroupBy(d => d.Name).ToList();

        foreach (var group in groups)
        {
            var items = group.ToList();

            if (items.Count == 1)
            {
                var d       = items[0];
                bool active = activeWindowsIds.Contains(d.WindowsId);
                Console.ForegroundColor = active ? ConsoleColor.Cyan : ConsoleColor.DarkGray;
                Console.WriteLine($"  {(active ? "►" : " ")} [{d.Index,2}] {d.Name}");
            }
            else
            {
                bool anyActive = items.Any(d => activeWindowsIds.Contains(d.WindowsId));
                Console.ForegroundColor = anyActive ? ConsoleColor.Cyan : ConsoleColor.DarkGray;
                Console.WriteLine($"  {(anyActive ? "►" : " ")}  {group.Key}  ({items.Count} endpoints)");

                foreach (var chunk in items.Chunk(10))
                {
                    Console.Write("       ");
                    foreach (var d in chunk)
                    {
                        bool active = activeWindowsIds.Contains(d.WindowsId);
                        Console.ForegroundColor = active ? ConsoleColor.Cyan : ConsoleColor.DarkGray;
                        Console.Write($"[{d.Index}] ");
                    }
                    Console.WriteLine();
                }
            }
        }

        Console.ResetColor();
        Console.WriteLine();
    }

    public void Render()
    {
        int row = _renderTop;

        foreach (var ch in _channels)
        {
            var device = _resolved.GetValueOrDefault(ch.DeviceName);

            WriteName(row++, ch.Name, ch.Gpi);

            bool failed = device is not null && _monitor.IsFailed(device.WindowsId);

            if (ch.ChannelIndex.Length == 1)
            {
                float db = (!failed && device is not null) ? _monitor.GetLevel(device.WindowsId, ch.ChannelIndex[0] - 1) : -96f;
                WriteBar(row++, "M", db, failed);
            }
            else
            {
                float dbL = (!failed && device is not null) ? _monitor.GetLevel(device.WindowsId, ch.ChannelIndex[0] - 1) : -96f;
                float dbR = (!failed && device is not null) ? _monitor.GetLevel(device.WindowsId, ch.ChannelIndex[1] - 1) : -96f;
                WriteBar(row++, "L", dbL, failed);
                WriteBar(row++, "R", dbR, failed);
            }

            WriteSubtitle(row++, device, ch.ChannelIndex, failed);
        }

        Console.SetCursorPosition(0, row);
        Console.Write(new string('─', 62));

        if (_dhd is not null)
            RenderDebug(row + 1);
    }

    private void RenderDebug(int startRow)
    {
        if (_debugLineCount <= 0) return;

        int maxRow  = Console.WindowHeight - 1;
        var frames  = _dhd!.GetRecentFrames();
        int frameSlots = _debugLineCount - 1; // 1 ligne réservée au header

        if (startRow > maxRow) return;
        Console.SetCursorPosition(0, startRow);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"  DHD debug ({frames.Count}/{DhdDriver.MaxRecentFrames})".PadRight(62));

        for (int i = 0; i < frameSlots; i++)
        {
            int row = startRow + 1 + i;
            if (row > maxRow) break;
            Console.SetCursorPosition(0, row);

            if (i >= frames.Count)
            {
                Console.Write(new string(' ', 62));
                continue;
            }

            var f = frames[frames.Count - 1 - i]; // plus récent en haut

            bool isLogicState = f.MsgId == unchecked((int)0x110E0000);
            Console.ForegroundColor = f.IsOpen ? ConsoleColor.Red : ConsoleColor.DarkGray;
            Console.Write("  ● ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            string rawInfo = isLogicState
                ? $"state:{f.RawValue}"
                : $"level:{f.RawValue,5}";
            Console.Write($"{f.ReceivedAt:HH:mm:ss.fff}  GPI:{f.Address,5}  {rawInfo}  → ");
            Console.ForegroundColor = f.IsOpen ? ConsoleColor.Red : ConsoleColor.DarkGray;
            Console.Write(f.IsOpen ? "ON " : "OFF");
            Console.ResetColor();
            Console.Write(new string(' ', 8));
        }

        Console.ResetColor();
    }

    private void WriteName(int row, string name, GpiConfig? gpi)
    {
        Console.SetCursorPosition(0, row);
        Console.ResetColor();
        Console.Write($"  {name}");

        if (gpi is not null && _dhd is not null && gpi.Mode == "dhd")
        {
            bool? isOpen   = _dhd.GetState(gpi.OnOff);
            bool  connected = _dhd.IsConnected;

            ConsoleColor dotColor = (!connected || isOpen is null)
                ? ConsoleColor.DarkGray
                : isOpen.Value ? ConsoleColor.Red : ConsoleColor.DarkGray;

            Console.Write(' ');
            Console.ForegroundColor = dotColor;
            Console.Write('●');
            Console.ResetColor();
            Console.Write(new string(' ', Math.Max(0, 62 - 2 - name.Length - 2)));
        }
        else
        {
            Console.Write(new string(' ', Math.Max(0, 62 - 2 - name.Length)));
        }
    }

    private static void WriteBar(int row, string side, float db, bool failed = false)
    {
        Console.SetCursorPosition(0, row);

        float clamped = Math.Clamp(db, MinDb, MaxDb);
        int filled = (int)((clamped - MinDb) / (MaxDb - MinDb) * BarWidth);
        int empty  = BarWidth - filled;

        string bar   = new string('█', filled) + new string('░', empty);
        string dbStr = db <= -95f ? "  -∞  " : $"{db,6:F1}";

        Console.Write($"   {side} ");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("│");
        if (failed)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
        }
        else
        {
            Console.ForegroundColor = GetColor(db) switch
            {
                "green"  => ConsoleColor.Green,
                "yellow" => ConsoleColor.Yellow,
                _        => ConsoleColor.Red
            };
        }
        Console.Write(bar);
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("│");
        Console.ResetColor();
        Console.Write($" {dbStr} dBFS  ");
    }

    private static void WriteSubtitle(int row, DeviceInfo? device, int[] channelIndex, bool failed = false)
    {
        Console.SetCursorPosition(0, row);
        Console.ForegroundColor = failed ? ConsoleColor.DarkRed : ConsoleColor.DarkGray;

        string chInfo = $"ch.[{string.Join(",", channelIndex)}]";

        string text = device is null
            ? "    ✘ périphérique introuvable"
            : failed
                ? $"    ✘ périphérique en mode exclusif  [{device.Index,2}] {device.Name}"
                : $"    [{device.Index,2}] {device.Name}  ({chInfo})";

        Console.Write(text);
        Console.ResetColor();
        Console.Write(new string(' ', Math.Max(0, 62 - text.Length)));
    }

    private static string GetColor(float db) =>
        db < -18f ? "green" : db < -6f ? "yellow" : "red";
}
