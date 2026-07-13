using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using HardwareMonitor.Core.Power;
using Microsoft.Win32;

namespace HardwareMonitor.App;

/// <summary>
/// Yhdistää Windowsin virta- ja istuntoilmoitukset yhdeksi
/// PowerSessionEvent-virraksi overlayn uudelleenluontia varten (ks.
/// OverlayRecoveryPolicy). Näytön sammuminen/herääminen tulee
/// WM_POWERBROADCAST-viestinä (GUID_CONSOLE_DISPLAY_STATE), lepotila ja
/// istunnon lukitus SystemEventsistä. Tapahtumat voivat laueta eri
/// säikeissä — kuuntelijan vastuulla on siirtyä UI-säikeelle.
/// </summary>
public sealed class PowerSessionEventSource : IDisposable
{
    private const int WmPowerBroadcast = 0x0218;
    private const int PbtPowerSettingChange = 0x8013;

    /// <summary>GUID_CONSOLE_DISPLAY_STATE; Data: 0 = pois, 1 = päällä, 2 = himmennetty.</summary>
    private static readonly Guid ConsoleDisplayState = new("6FE69556-704A-47A0-8F24-C28D936FDA47");

    private readonly HwndSource _source;
    private readonly nint _displayRegistration;
    private bool _disposed;

    public event Action<PowerSessionEvent>? EventReceived;

    public PowerSessionEventSource(Window window)
    {
        // Trayhin käynnistyttäessä pääikkunaa ei näytetä — kahva luodaan
        // silti, jotta WM_POWERBROADCAST-ilmoitukset saadaan vastaan
        // (piilotettukin ylätason ikkuna vastaanottaa ne).
        nint handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle)!;
        _source.AddHook(WndProc);

        Guid guid = ConsoleDisplayState;
        _displayRegistration = RegisterPowerSettingNotification(handle, ref guid, 0);

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmPowerBroadcast && wParam == PbtPowerSettingChange && lParam != 0)
        {
            var setting = Marshal.PtrToStructure<PowerBroadcastSetting>(lParam);
            if (setting.PowerSetting == ConsoleDisplayState)
            {
                EventReceived?.Invoke(setting.Data switch
                {
                    0 => PowerSessionEvent.DisplayOff,
                    2 => PowerSessionEvent.DisplayDimmed,
                    _ => PowerSessionEvent.DisplayOn,
                });
            }
        }

        return 0;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            EventReceived?.Invoke(PowerSessionEvent.Suspend);
        }
        else if (e.Mode == PowerModes.Resume)
        {
            EventReceived?.Invoke(PowerSessionEvent.Resume);
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
                EventReceived?.Invoke(PowerSessionEvent.SessionLock);
                break;

            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
                EventReceived?.Invoke(PowerSessionEvent.SessionUnlock);
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;

        if (_displayRegistration != 0)
        {
            _ = UnregisterPowerSettingNotification(_displayRegistration);
        }

        _source.RemoveHook(WndProc);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PowerBroadcastSetting
    {
        public Guid PowerSetting;
        public uint DataLength;
        public byte Data;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint RegisterPowerSettingNotification(nint hRecipient, ref Guid powerSettingGuid, uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnregisterPowerSettingNotification(nint handle);
}
