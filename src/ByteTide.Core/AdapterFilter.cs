using System.Net.NetworkInformation;

namespace ByteTide.Core;

/// <summary>
/// Decides which network interfaces count towards "All Connections".
/// Virtual, VPN and tunnel adapters carry traffic that is ALSO counted on the
/// physical adapter underneath them, so including them double-counts data.
/// </summary>
public static class AdapterFilter
{
    private static readonly string[] VirtualNameMarkers =
    {
        "Hyper-V", "vEthernet", "Virtual", "VMware", "VirtualBox", "Npcap", "TAP-", "Wintun",
        "WireGuard", "NordLynx", "OpenVPN", "Pseudo-Interface", "Loopback",
        "Miniport (IKEv2)", "Miniport (L2TP)", "Miniport (PPTP)", "Miniport (SSTP)"
    };

    /// <summary>True for adapters that are normally virtual / VPN / tunnels.</summary>
    public static bool LooksVirtual(NetworkInterface nic)
    {
        if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) return true;

        var description = nic.Description ?? "";
        foreach (var marker in VirtualNameMarkers)
            if (description.Contains(marker, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }
}
