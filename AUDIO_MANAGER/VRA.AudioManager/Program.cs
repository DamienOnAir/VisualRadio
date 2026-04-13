using System.Text.Json;
using NAudio.CoreAudioApi;
using VRA.AudioManager.Audio;
using VRA.AudioManager.Config;
using VRA.AudioManager.Display;
using VRA.AudioManager.Gpio;

namespace VRA.AudioManager;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var configPath = Path.Combine(AppContext.BaseDirectory, "vra-audio.json");
        if (!File.Exists(configPath))
        {
            Console.WriteLine($"  ✘ Fichier de configuration introuvable : {configPath}");
            return;
        }

        var config = JsonSerializer.Deserialize<AudioConfig>(File.ReadAllText(configPath), JsonOptions);
        if (config is null || config.Channels.Count == 0)
        {
            Console.WriteLine("  ✘ Configuration invalide ou aucun canal défini.");
            return;
        }

        using var enumerator = new MMDeviceEnumerator();

        // Tri naturel → indices stables et groupés par carte
        var rawDevices = enumerator
            .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .OrderBy(d => d.FriendlyName, NaturalStringComparer.Instance)
            .ToList();

        var allDevices = rawDevices
            .Select((d, i) => new DeviceInfo(i + 1, d.FriendlyName, d.ID))
            .ToList();

        // Résolution DeviceName (config) → DeviceInfo par correspondance partielle
        var resolved = new Dictionary<string, DeviceInfo>();
        var errors   = new List<string>();

        foreach (var ch in config.Channels)
        {
            if (resolved.ContainsKey(ch.DeviceName)) continue;

            var match = allDevices.FirstOrDefault(d =>
                d.Name.Contains(ch.DeviceName, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
                resolved[ch.DeviceName] = match;
            else
                errors.Add($"  ✘ Périphérique introuvable pour \"{ch.DeviceName}\"");
        }

        if (errors.Count > 0)
        {
            errors.ForEach(Console.WriteLine);
            Console.WriteLine("  Périphériques disponibles :");
            allDevices.ForEach(d => Console.WriteLine($"    [{d.Index,2}] {d.Name}"));
            return;
        }

        // Ouverture des seuls devices nécessaires (clé = Windows Device ID)
        var devicesToOpen = resolved.Values
            .DistinctBy(d => d.WindowsId)
            .ToDictionary(d => d.WindowsId, d => rawDevices[d.Index - 1]);

        using var monitor = new LevelMonitor(devicesToOpen);

        DhdDriver? dhd = null;
        if (config.Dhd is not null)
        {
            dhd = new DhdDriver(config.Dhd.Ip, config.Dhd.Port);
            dhd.ConnectionChanged += connected =>
                Console.Title = connected ? "VRA Audio — DHD connecté" : "VRA Audio — DHD déconnecté";

            foreach (var ch in config.Channels)
                if (ch.Gpi?.Mode == "dhd")
                    dhd.WatchAddress(ch.Gpi.OnOff);

            dhd.Start();
        }

        var renderer = new VuMeterRenderer(config.Channels, monitor, allDevices, resolved, dhd);

        renderer.PrintHeader();
        monitor.Start();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                renderer.Render();
            }
            catch (System.IO.IOException)
            {
                // Pipe console cassé (ex: terminal fermé) — on quitte proprement
                break;
            }
            Thread.Sleep(30);
        }

        monitor.Stop();
        dhd?.Dispose();
        Console.CursorVisible = true;
        Console.WriteLine("\n\n  Arrêt.");
    }
}
