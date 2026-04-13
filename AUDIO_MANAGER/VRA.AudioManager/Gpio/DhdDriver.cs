using System.Buffers.Binary;
using System.Net.Sockets;

namespace VRA.AudioManager.Gpio;

/// <summary>Trame DHD reçue, pour le debug temps réel.</summary>
/// <param name="RawValue">Pour SetLogicState : byte state (0/1). Pour SetLogicRequest : niveau uint16 (0–10000).</param>
public record DhdFrame(DateTime ReceivedAt, int MsgId, short Address, ushort RawValue, bool IsOpen);

/// <summary>
/// Client TCP pour le protocole binaire DHD (GPIO / Logic States).
/// Blocs de 16 octets :
///   [0]    dataLen — nb d'octets significatifs après le msgId (0x02 = requête, 0x03 = état)
///   [1]    0x00
///   [2-5]  message ID  (big-endian int32)
///             0x110E0000 = SetLogicState  (DHD → nous, dataLen=3) — état binaire ON/OFF
///             0x11030000 = SetLogicRequest (nous → DHD, dataLen=2) / réponse DHD (dataLen=4, level uint16)
///   [6-7]  GPI address (big-endian int16)
///   [8]    state  (SetLogicState uniquement) — 1 = ON (fader ouvert), 0 = OFF (fader fermé)
///   [8-9]  level  (SetLogicRequest réponse) — uint16 big-endian, 0=muet, 10000=0dB
///   [9-15] padding
/// </summary>
public class DhdDriver : IDisposable
{
    private const int    BlockSize           = 16;
    private const int    MsgSetLogicState    = 0x110E0000;
    private const int    ReconnectDelayMs    = 3000;
    public  const int    MaxRecentFrames     = 12;

    private readonly string       _ip;
    private readonly int          _port;
    private readonly CancellationTokenSource _cts = new();

    private readonly Dictionary<short, bool> _states       = new();
    private readonly Queue<DhdFrame>         _recentFrames = new();
    private readonly object _lock = new();

    // Adresses dont on veut connaître l'état initial au connect
    private readonly HashSet<short> _watchedAddresses = new();

    /// <summary>Fired quand un état logique GPI change. args : adresse GPI, isOpen (true = fader ouvert).</summary>
    public event Action<short, bool>? GpiStateChanged;

    /// <summary>Fired quand la connexion DHD change. args : isConnected.</summary>
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected { get; private set; }

    public DhdDriver(string ip, int port)
    {
        _ip   = ip;
        _port = port;
    }

    /// <summary>Enregistre une adresse GPI pour interroger son état logique initial à la connexion.</summary>
    public void WatchAddress(short address) => _watchedAddresses.Add(address);

    public void Start()
    {
        Task.Run(() => RunLoop(_cts.Token));
    }

    /// <summary>Retourne l'état logique courant d'un GPI. true = ON/on-air, false = OFF, null = inconnu.</summary>
    public bool? GetState(short gpiAddress)
    {
        lock (_lock)
            return _states.TryGetValue(gpiAddress, out var v) ? v : null;
    }

/// <summary>Retourne une copie snapshot des dernières trames reçues (les plus récentes en dernier).</summary>
    public IReadOnlyList<DhdFrame> GetRecentFrames()
    {
        lock (_lock)
            return _recentFrames.ToList();
    }

    private async Task RunLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(_ip, _port, ct);

                IsConnected = true;
                ConnectionChanged?.Invoke(true);

                var stream = client.GetStream();

                // Requête 0x110E0000 + dataLen=2 pour récupérer l'état logique initial.
                foreach (var addr in _watchedAddresses)
                    await SendLogicStateQueryAsync(stream, addr, ct);

                await ReadLoop(stream, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // connexion perdue ou refusée — on réessaie
            }

            IsConnected = false;
            ConnectionChanged?.Invoke(false);

            if (!ct.IsCancellationRequested)
                await Task.Delay(ReconnectDelayMs, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Envoie une requête d'état logique (0x110E0000, dataLen=2) — pattern symétrique à SetLogicRequest.
    /// Si le DHD répond, ProcessBlock recevra un 0x110E0000 avec l'état binaire courant.
    /// </summary>
    private static async Task SendLogicStateQueryAsync(NetworkStream stream, short address, CancellationToken ct)
    {
        var frame = new byte[BlockSize];
        frame[0] = 0x02;  // dataLen=2 (requête, pas d'octet état)
        frame[1] = 0x00;
        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(2), MsgSetLogicState);  // 0x110E0000
        BinaryPrimitives.WriteInt16BigEndian(frame.AsSpan(6), address);
        // bytes [8-15] = 0x00 (padding)
        await stream.WriteAsync(frame, ct);
    }

    private async Task ReadLoop(NetworkStream stream, CancellationToken ct)
    {
        var buffer    = new byte[BlockSize * 16]; // lecture par paquets
        var carry     = new List<byte>();

        while (!ct.IsCancellationRequested)
        {
            int read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break; // connexion fermée

            carry.AddRange(buffer.AsSpan(0, read).ToArray());

            while (carry.Count >= BlockSize)
            {
                ProcessBlock(carry.GetRange(0, BlockSize).ToArray());
                carry.RemoveRange(0, BlockSize);
            }
        }
    }

    private void ProcessBlock(byte[] block)
    {
        int   id      = BinaryPrimitives.ReadInt32BigEndian(block.AsSpan(2));
        short address = BinaryPrimitives.ReadInt16BigEndian(block.AsSpan(6));
        byte  state   = block[8];

        if (id != MsgSetLogicState) return;

        bool   isOpen   = state == 1;
        ushort rawValue = state;

        bool changed;
        lock (_lock)
        {
            changed = !_states.TryGetValue(address, out var prev) || prev != isOpen;
            _states[address] = isOpen;

            _recentFrames.Enqueue(new DhdFrame(DateTime.Now, id, address, rawValue, isOpen));
            if (_recentFrames.Count > MaxRecentFrames)
                _recentFrames.Dequeue();
        }

        if (changed)
            GpiStateChanged?.Invoke(address, isOpen);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
