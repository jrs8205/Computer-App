using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HardwareMonitor.App.Services;

/// <summary>
/// Varmistaa ladatun tiedoston Authenticode-allekirjoituksen (WinVerifyTrust)
/// JA että allekirjoittaja on täsmälleen oma julkaisuvarmenteemme
/// (thumbprint-pinnaus) — pelkkä "joku luotettu allekirjoitus" ei riitä.
/// </summary>
public static class AuthenticodeVerifier
{
    /// <summary>CN=jrs8205 Hardware Monitor (voimassa 2031, luotettu koneen Rootissa).</summary>
    public const string ExpectedThumbprint = "346D869550F3A7BD54FA947E024341C64F729AF8";

    public static bool IsValid(string filePath, Action<string> log)
    {
        if (!VerifyTrust(filePath))
        {
            log($"Päivityksen allekirjoitus ei kelpaa: {filePath}");
            return false;
        }

        try
        {
            using var cert = new X509Certificate2(
                X509Certificate.CreateFromSignedFile(filePath));
            bool match = string.Equals(
                cert.Thumbprint, ExpectedThumbprint, StringComparison.OrdinalIgnoreCase);
            if (!match)
            {
                log($"Päivityksen allekirjoittaja on väärä: {cert.Thumbprint}");
            }

            return match;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            log($"Päivityksen varmenteen luku epäonnistui: {ex.Message}");
            return false;
        }
    }

    private static readonly Guid ActionGenericVerifyV2 =
        new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private static bool VerifyTrust(string filePath)
    {
        IntPtr fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WintrustFileInfo>());
        try
        {
            var fileInfo = new WintrustFileInfo
            {
                cbStruct = (uint)Marshal.SizeOf<WintrustFileInfo>(),
                pcwszFilePath = filePath,
            };
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, fDeleteOld: false);

            var data = new WintrustData
            {
                cbStruct = (uint)Marshal.SizeOf<WintrustData>(),
                dwUIChoice = 2,          // WTD_UI_NONE — ei dialogeja
                fdwRevocationChecks = 0, // WTD_REVOKE_NONE — itse allekirjoitettu, ei CRL:ää
                dwUnionChoice = 1,       // WTD_CHOICE_FILE
                pFile = fileInfoPtr,
            };
            Guid action = ActionGenericVerifyV2;
            return WinVerifyTrust(IntPtr.Zero, ref action, ref data) == 0;
        }
        finally
        {
            Marshal.FreeHGlobal(fileInfoPtr);
        }
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(
        IntPtr hwnd, ref Guid actionId, ref WintrustData data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WintrustFileInfo
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WintrustData
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }
}
