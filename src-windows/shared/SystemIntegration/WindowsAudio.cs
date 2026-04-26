using System.Runtime.InteropServices;

namespace Gnd.Windows.Shared.SystemIntegration;

/// <summary>
/// Windows Audio management using WASAPI
/// </summary>
public class WindowsAudio : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the default audio playback device
    /// </summary>
    public string? GetDefaultPlaybackDevice()
    {
        try
        {
            // Use MMDevice API via COM
            var deviceEnumerator = (IMMDeviceEnumerator?)Activator.CreateInstance(
                Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))!);

            if (deviceEnumerator == null) return null;

            deviceEnumerator.GetDefaultAudioEndpoint(
                EDataFlow.eRender,
                ERole.eConsole,
                out IMMDevice? device);

            if (device != null)
            {
                device.GetId(out string? deviceId);
                Marshal.ReleaseComObject(device);
                return deviceId;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get default audio device: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Gets a list of all audio playback devices
    /// </summary>
    public List<AudioDevice> GetPlaybackDevices()
    {
        var devices = new List<AudioDevice>();

        try
        {
            var deviceEnumerator = (IMMDeviceEnumerator?)Activator.CreateInstance(
                Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))!);

            if (deviceEnumerator == null) return devices;

            deviceEnumerator.EnumAudioEndpoints(
                EDataFlow.eRender,
                DEVICE_STATE.MASK_ALL,
                out IMMDeviceCollection? collection);

            if (collection != null)
            {
                collection.GetCount(out uint count);
                for (int i = 0; i < count; i++)
                {
                    collection.Item((uint)i, out IMMDevice? device);
                    if (device != null)
                    {
                        device.GetId(out string? id);
                        device.OpenPropertyStore(STGM_ACCESS.STGM_READ, out IPropertyStore? store);
                        if (store != null)
                        {
                            var propVar = new PropVariant();
                            store.GetValue(PKEY.Dev_FriendlyName, ref propVar);
                            devices.Add(new AudioDevice
                            {
                                Id = id ?? "",
                                Name = propVar.Value?.ToString() ?? "Unknown",
                                IsDefault = i == 0
                            });
                            Marshal.ReleaseComObject(store);
                        }
                        Marshal.ReleaseComObject(device);
                    }
                }
                Marshal.ReleaseComObject(collection);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enumerate audio devices: {ex.Message}");
        }

        return devices;
    }

    /// <summary>
    /// Set the audio output to a specific device
    /// </summary>
    public bool SetPlaybackDevice(string deviceId)
    {
        // This requires setting the default device via policy config
        // For now, we return false as this is complex
        System.Diagnostics.Debug.WriteLine("SetPlaybackDevice not fully implemented");
        return false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

public class AudioDevice
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsDefault { get; set; }
}

// COM Interfaces for MMDevice API
#region MMDevice COM

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    void NotImpl1();
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
    int EnumAudioEndpoints(EDataFlow dataFlow, DEVICE_STATE stateMask, out IMMDeviceCollection ppDevices);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    int Activate(ref Guid iid, CLSCTX dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    int OpenPropertyStore(STGM_ACCESS access, out IPropertyStore ppProperties);
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
    int GetState(out DEVICE_STATE pdwState);
}

[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    int GetCount(out uint pcDevices);
    int Item(uint nDevice, out IMMDevice ppDevice);
}

[ComImport]
[Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    int GetCount(out int cProps);
    int GetAt(int iProp, out PropertyKey pkey);
    int GetValue(ref PropertyKey key, ref PropVariant pv);
    int SetValue(ref PropertyKey key, ref PropVariant pv);
    int Commit();
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropVariant
{
    public ushort vt;
    public ushort wReserved1;
    public ushort wReserved2;
    public ushort wReserved3;
    public IntPtr pointerValue;

    public object? Value
    {
        get
        {
            return vt switch
            {
                31 => Marshal.PtrToStringUni(pointerValue), // VT_LPWSTR
                13 => Marshal.GetObjectForIUnknown(pointerValue), // VT_UNKNOWN
                _ => null
            };
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey
{
    public Guid fmtid;
    public int pid;

    public PropertyKey(Guid fmtid, int pid)
    {
        this.fmtid = fmtid;
        this.pid = pid;
    }
}

internal enum EDataFlow
{
    eRender = 0,
    eCapture = 1,
    eAll = 2,
}

internal enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2,
}

[Flags]
internal enum DEVICE_STATE
{
    MASK_ALL = 0x7FFFFFFF,
}

internal enum CLSCTX
{
    INPROC_SERVER = 0x1,
}

[Flags]
internal enum STGM_ACCESS
{
    STGM_READ = 0x0,
}

internal static class PKEY
{
    public static readonly PropertyKey Dev_FriendlyName = new(
        new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 2);
}

#endregion
