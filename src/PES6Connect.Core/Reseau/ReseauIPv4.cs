using System.Net;
using System.Net.Sockets;

namespace PES6Connect.Core.Reseau;

/// <summary>Calculs simples sur une adresse du type « 10.134.125.26/24 ».</summary>
public sealed record ReseauIPv4(IPAddress Adresse, int Prefixe)
{
    public static ReseauIPv4? Lire(string cidr)
    {
        var morceaux = cidr.Split('/');
        if (morceaux.Length != 2
            || !IPAddress.TryParse(morceaux[0], out var ip) || ip.AddressFamily != AddressFamily.InterNetwork
            || !int.TryParse(morceaux[1], out var prefixe) || prefixe is < 8 or > 30)
        {
            return null;
        }
        return new ReseauIPv4(ip, prefixe);
    }

    private uint Masque => Prefixe == 0 ? 0 : uint.MaxValue << (32 - Prefixe);

    /// <summary>Adresse qui atteint tous les PC du réseau (ex. 10.134.125.255).</summary>
    public IPAddress Diffusion => VersAdresse((VersEntier(Adresse) & Masque) | ~Masque);

    public bool Contient(IPAddress ip) =>
        ip.AddressFamily == AddressFamily.InterNetwork && (VersEntier(ip) & Masque) == (VersEntier(Adresse) & Masque);

    private static uint VersEntier(IPAddress ip)
    {
        var o = ip.GetAddressBytes();
        return ((uint)o[0] << 24) | ((uint)o[1] << 16) | ((uint)o[2] << 8) | o[3];
    }

    private static IPAddress VersAdresse(uint v) =>
        new([(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v]);
}
