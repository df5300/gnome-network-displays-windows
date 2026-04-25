using System.Net;
using System.Net.Sockets;

namespace GnomeNetworkDisplays.Stream.Media;

public class TransportStreamReceiver : IDisposable
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private IPEndPoint? _remoteEndPoint;

    // TS packet constants
    private const int TsPacketSize = 188;
    private const int TsSyncByte = 0x47;

    // PID definitions
    private const ushort PidPat = 0x0000;  // Program Association Table
    private const ushort PidPmt = 0x1000; // Program Map Table
    private const ushort PidVideo = 0x100;
    private const ushort PidAudio = 0x101;

    // State
    private readonly object _lock = new();
    private readonly byte[] _packetBuffer = new byte[TsPacketSize];
    private int _bufferIndex = 0;
    private bool _isReceiving;

    // Callbacks
    public event Action<byte[]>? OnTsPacketReceived;
    public event Action<PatSection>? OnPatReceived;
    public event Action<PmtSection>? OnPmtReceived;

    // Parsed PSI tables
    public PatSection? CurrentPat { get; private set; }
    public PmtSection? CurrentPmt { get; private set; }

    public bool IsReceiving => _isReceiving;

    public async Task StartAsync(string host, int port, CancellationToken cancellationToken)
    {
        _remoteEndPoint = new IPEndPoint(IPAddress.Parse(host), port);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Allow binding to the same port for multicast
        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        // Check if this is multicast
        if (IPAddress.IsLoopback(_remoteEndPoint.Address) ||
            IsMulticastAddress(_remoteEndPoint.Address))
        {
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            if (IsMulticastAddress(_remoteEndPoint.Address))
            {
                _udpClient.JoinMulticastGroup(_remoteEndPoint.Address);
            }
        }
        else
        {
            await _udpClient.ConnectAsync(_remoteEndPoint, cancellationToken);
        }

        _isReceiving = true;
        _receiveTask = ReceiveLoopAsync(_cts.Token);

        Console.WriteLine($"Started UDP receiving on {host}:{port}");
    }

    public void Stop()
    {
        _isReceiving = false;
        _cts?.Cancel();

        if (_udpClient != null && _remoteEndPoint != null &&
            IsMulticastAddress(_remoteEndPoint.Address))
        {
            try
            {
                _udpClient.DropMulticastGroup(_remoteEndPoint.Address);
            }
            catch
            {
                // Ignore errors when leaving multicast group
            }
        }
    }

    public void FeedPacket(byte[] rtpPacket)
    {
        // Extract RTP payload (skip RTP header - typically 12 bytes + CSRC)
        int payloadOffset = 12; // Basic RTP header
        int payloadLength = rtpPacket.Length - payloadOffset;

        // Process the RTP payload as TS packets
        for (int i = payloadOffset; i + TsPacketSize <= rtpPacket.Length; i += TsPacketSize)
        {
            byte[] tsPacket = new byte[TsPacketSize];
            Array.Copy(rtpPacket, i, tsPacket, 0, TsPacketSize);
            ProcessTsPacket(tsPacket);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[7 * TsPacketSize]; // Buffer for multiple TS packets

        while (!cancellationToken.IsCancellationRequested && _isReceiving)
        {
            try
            {
                if (_udpClient != null)
                {
                    var result = await _udpClient.ReceiveAsync(buffer, cancellationToken);
                    int bytesReceived = result.Buffer.Length;

                    // Process received bytes
                    ProcessReceivedData(result.Buffer, bytesReceived);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UDP receive error: {ex.Message}");
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private void ProcessReceivedData(byte[] data, int length)
    {
        int offset = 0;

        // Sync to TS packet boundary
        while (offset < length && data[offset] != TsSyncByte)
        {
            offset++;
        }

        // Process complete TS packets
        while (offset + TsPacketSize <= length)
        {
            if (data[offset] == TsSyncByte)
            {
                byte[] packet = new byte[TsPacketSize];
                Array.Copy(data, offset, packet, 0, TsPacketSize);
                ProcessTsPacket(packet);
                offset += TsPacketSize;
            }
            else
            {
                offset++;
            }
        }
    }

    private void ProcessTsPacket(byte[] packet)
    {
        if (packet.Length < TsPacketSize)
            return;

        // Parse TS header
        int syncByte = packet[0];
        if (syncByte != TsSyncByte)
            return;

        bool transportErrorIndicator = (packet[1] & 0x80) != 0;
        bool payloadUnitStartIndicator = (packet[1] & 0x40) != 0;
        byte transportPriority = (byte)((packet[1] & 0x20) >> 5);

        ushort pid = (ushort)(((packet[1] & 0x1F) << 8) | packet[2]);

        byte transportScramblingControl = (byte)((packet[3] & 0xC0) >> 6);
        byte adaptationFieldControl = (byte)((packet[3] & 0x30) >> 4);
        byte continuityCounter = (byte)(packet[3] & 0x0F);

        // Skip transport error indicator packets
        if (transportErrorIndicator)
            return;

        // Calculate payload offset
        int payloadOffset = 4;
        int payloadLength = TsPacketSize - 4;

        // Handle adaptation field
        if ((adaptationFieldControl & 0x02) != 0)
        {
            // Adaptation field present
            int adaptationFieldLength = packet[4];
            payloadOffset += 1 + adaptationFieldLength;
            payloadLength -= 1 + adaptationFieldLength;

            if (payloadLength < 0)
                return;
        }

        if ((adaptationFieldControl & 0x01) == 0)
        {
            // No payload in this packet
            return;
        }

        // Handle payload unit start indicator (for PSI data)
        if (payloadUnitStartIndicator && payloadLength > 0)
        {
            int pointerField = packet[payloadOffset];
            payloadOffset++;
            payloadLength--;

            if (pointerField > payloadLength)
                return;

            // Process PSI section if pointer is 0
            if (pointerField == 0)
            {
                ProcessPsiSection(packet, payloadOffset, payloadLength, pid);
            }
            else
            {
                // Skip to the new section and process it
                ProcessPsiSection(packet, payloadOffset + pointerField,
                    payloadLength - pointerField, pid);
            }
        }

        // Pass packet to callback
        OnTsPacketReceived?.Invoke(packet);
    }

    private void ProcessPsiSection(byte[] packet, int offset, int length, ushort pid)
    {
        if (length < 3)
            return;

        int tableId = packet[offset];
        // Skip section_syntax_indicator (bit 7), private_bit (bit 6), reserved (bits 5-4)
        // ushort sectionLength = (ushort)((packet[offset + 1] & 0x0F) << 8 | packet[offset + 2]);

        switch (pid)
        {
            case PidPat:
                ParsePat(packet, offset, length);
                break;

            case PidPmt:
                ParsePmt(packet, offset, length);
                break;
        }
    }

    private void ParsePat(byte[] data, int offset, int length)
    {
        if (length < 8)
            return;

        try
        {
            // PAT structure:
            // table_id (1 byte) - always 0x00 for PAT
            // section_syntax_indicator (1 bit) + private_bit (1 bit) + reserved (2 bits) + section_length (12 bits)
            // transport_stream_id (2 bytes)
            // reserved (2 bits) + version_number (5 bits) + current_next_indicator (1 bit)
            // section_number (1 byte)
            // last_section_number (1 byte)
            // [programs] - each is program_number (2 bytes) + reserved (3 bits) + PID (13 bits)
            // CRC (4 bytes)

            int sectionLength = ((data[offset + 1] & 0x0F) << 8) | data[offset + 2];
            int programCount = (sectionLength - 9) / 4;

            var pat = new PatSection();

            int pos = offset + 8; // Skip to after transport_stream_id, version, section_num, last_section_num

            for (int i = 0; i < programCount && pos + 4 <= offset + 3 + sectionLength - 4; i++)
            {
                ushort programNumber = (ushort)((data[pos] << 8) | data[pos + 1]);
                ushort programPid = (ushort)(((data[pos + 2] & 0x1F) << 8) | data[pos + 3]);

                if (programNumber == 0)
                {
                    pat.NetworkPid = programPid;
                }
                else
                {
                    pat.ProgramPids[programNumber] = programPid;
                }

                pos += 4;
            }

            lock (_lock)
            {
                CurrentPat = pat;
            }

            Console.WriteLine($"PAT received: Network PID = {pat.NetworkPid}, Programs = {pat.ProgramPids.Count}");
            OnPatReceived?.Invoke(pat);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing PAT: {ex.Message}");
        }
    }

    private void ParsePmt(byte[] data, int offset, int length)
    {
        if (length < 4)
            return;

        try
        {
            // PMT structure similar to PAT
            // After section headers:
            // PCR_PID (13 bits) + reserved (4 bits)
            // program_info_length (12 bits)
            // [descriptors]
            // [stream_type + PID info]...

            int sectionLength = ((data[offset + 1] & 0x0F) << 8) | data[offset + 2];
            int pos = offset + 4;

            ushort pcrPid = (ushort)(((data[pos] & 0x1F) << 8) | data[pos + 1]);
            pos += 2;

            int programInfoLength = ((data[pos] & 0x0F) << 8) | data[pos + 1];
            pos += 2 + programInfoLength;

            var pmt = new PmtSection
            {
                PcrPid = pcrPid
            };

            while (pos + 5 < offset + 3 + sectionLength - 4)
            {
                byte streamType = data[pos];
                ushort elementaryPid = (ushort)(((data[pos + 1] & 0x1F) << 8) | data[pos + 2]);
                int esInfoLength = ((data[pos + 3] & 0x0F) << 8) | data[pos + 4];

                pmt.Streams.Add(new PmtStreamInfo
                {
                    StreamType = streamType,
                    ElementaryPid = elementaryPid
                });

                pos += 5 + esInfoLength;
            }

            lock (_lock)
            {
                CurrentPmt = pmt;
            }

            Console.WriteLine($"PMT received: PCR PID = {pmt.PcrPid}, Streams = {pmt.Streams.Count}");
            OnPmtReceived?.Invoke(pmt);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing PMT: {ex.Message}");
        }
    }

    private static bool IsMulticastAddress(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] >= 224 && bytes[0] <= 239;
    }

    public void Dispose()
    {
        Stop();
        _udpClient?.Dispose();
        _cts?.Dispose();
    }
}

public class PatSection
{
    public ushort NetworkPid { get; set; }
    public Dictionary<ushort, ushort> ProgramPids { get; } = new();
}

public class PmtSection
{
    public ushort PcrPid { get; set; }
    public List<PmtStreamInfo> Streams { get; } = new();
}

public class PmtStreamInfo
{
    public byte StreamType { get; set; }
    public ushort ElementaryPid { get; set; }

    public string GetStreamTypeName()
    {
        return StreamType switch
        {
            0x01 => "MPEG-1 Video",
            0x02 => "MPEG-2 Video",
            0x03 => "MPEG-1 Audio",
            0x04 => "MPEG-2 Audio",
            0x06 => "MPEG-2 PES Private Data",
            0x0f => "AAC Audio (MPEG-2)",
            0x10 => "MPEG-4 Video (H.264/AVC)",
            0x1b => "H.264 Video",
            0x1f => "MPEG-4 Video (H.265/HEVC)",
            0x24 => "H.265/HEVC Video",
            0xdb => "MPEG-2 SLS",
            _ => $"Unknown (0x{StreamType:X2})"
        };
    }
}
