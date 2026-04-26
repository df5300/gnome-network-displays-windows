using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gnd.Windows.Service.Providers.Chromecast;

public class CastMessage
{
    public string SourceId { get; set; } = "sender-0";
    public string DestinationId { get; set; } = "receiver-0";
    public string Namespace { get; set; } = string.Empty;
    public CastMessageType Type { get; set; } = CastMessageType.kResult;
    public string Payload { get; set; } = string.Empty;
    public string PayloadType { get; set; } = "STRING";

    public byte[] ToProto()
    {
        // CASTV2 uses a simple binary protocol:
        // [version (1 byte)][message type (1 byte)][payload length (4 bytes big-endian)][payload]
        var payloadBytes = Encoding.UTF8.GetBytes(JsonSerialize());
        var message = new byte[6 + payloadBytes.Length];

        message[0] = 0x00; // Version
        message[1] = (byte)Type;
        message[2] = (byte)((payloadBytes.Length >> 24) & 0xFF);
        message[3] = (byte)((payloadBytes.Length >> 16) & 0xFF);
        message[4] = (byte)((payloadBytes.Length >> 8) & 0xFF);
        message[5] = (byte)(payloadBytes.Length & 0xFF);

        Buffer.BlockCopy(payloadBytes, 0, message, 6, payloadBytes.Length);

        return message;
    }

    public static CastMessage FromProto(byte[] data)
    {
        if (data.Length < 6)
            throw new ArgumentException("Invalid CastMessage: too short");

        var message = new CastMessage
        {
            Type = (CastMessageType)data[1]
        };

        var payloadLength = (data[2] << 24) | (data[3] << 16) | (data[4] << 8) | data[5];
        if (data.Length >= 6 + payloadLength)
        {
            var payload = Encoding.UTF8.GetString(data, 6, payloadLength);
            JsonDeserialize(payload, message);
        }

        return message;
    }

    private string JsonSerialize()
    {
        return JsonSerializer.Serialize(this, CastMessageJsonContext.Default.CastMessage);
    }

    private static void JsonDeserialize(string json, CastMessage message)
    {
        var parsed = JsonSerializer.Deserialize(json, CastMessageJsonContext.Default.CastMessage);
        if (parsed != null)
        {
            message.SourceId = parsed.SourceId;
            message.DestinationId = parsed.DestinationId;
            message.Namespace = parsed.Namespace;
            message.Type = parsed.Type;
            message.Payload = parsed.Payload;
            message.PayloadType = parsed.PayloadType;
        }
    }
}

public enum CastMessageType
{
    kBindTransportClient = 0,
    kReceiverStatus = 1,
    kLaunch = 2,
    kStarted = 3,
    kStopped = 4,
    kError = 5,
    kGetStatus = 6,
    kConnected = 7,
    kResult = 8
}

// Namespaces
public static class CastNamespaces
{
    public const string DeviceAuth = "urn:x-cast:com.google.cast.tp.deviceauth";
    public const string Connection = "urn:x-cast:com.google.cast.socket";
    public const string Heartbeat = "urn:x-cast:com.google.cast.heartbeat";
    public const string Receiver = "urn:x-cast:com.google.cast.receiver";
    public const string Media = "urn:x-cast:com.google.cast.media";
}

// JSON serialization context for System.Text.Json source generation
[JsonSerializable(typeof(CastMessage))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class CastMessageJsonContext : JsonSerializerContext
{
}
