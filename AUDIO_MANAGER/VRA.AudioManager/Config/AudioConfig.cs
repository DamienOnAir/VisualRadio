using System.Text.Json.Serialization;

namespace VRA.AudioManager.Config;

public class AudioConfig
{
    public DhdConfig? Dhd { get; init; }
    public List<ChannelConfig> Channels { get; init; } = [];
}

public class DhdConfig
{
    public string Ip   { get; init; } = "127.0.0.1";
    public int    Port { get; init; } = 2002;
}

/// <summary>
/// Configuration GPI d'un canal.
/// Si absent du JSON, aucune analyse GPI n'est effectuée pour ce canal.
/// </summary>
public class GpiConfig
{
    /// <summary>Mode GPI : "dhd" (seul mode supporté actuellement).</summary>
    public string Mode { get; init; } = "";

    /// <summary>Adresse GPI (adresse DHD on/off du fader).</summary>
    [JsonPropertyName("on_off")]
    public short OnOff { get; init; }
}

public class ChannelConfig
{
    /// <summary>Nom affiché sur le VU-mètre.</summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Nom (ou fragment) du périphérique WASAPI — correspondance partielle insensible à la casse.
    /// Ex : "MADI (1-8)" ou "Blackmagic DeckLink Duo 2 (3)".
    /// </summary>
    public string DeviceName { get; init; } = "";

    /// <summary>
    /// Numéro(s) 1-based des canaux à lire au sein du device WASAPI.
    /// [1]    → mono sur le canal 1
    /// [1, 2] → stéréo L=canal 1, R=canal 2
    /// </summary>
    public int[] ChannelIndex { get; init; } = [1];

    /// <summary>
    /// Configuration GPI pour ce canal.
    /// Si null, aucune analyse GPI n'est effectuée.
    /// </summary>
    public GpiConfig? Gpi { get; init; }
}
