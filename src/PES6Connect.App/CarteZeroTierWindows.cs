using System.Net;
using System.Net.Sockets;
using PES6Connect.Core.Reseau;

namespace PES6Connect.App;

/// <summary>Lit et corrige les réglages Windows de la carte réseau ZeroTier.</summary>
internal static class CarteZeroTierWindows
{
    /// <summary>
    /// Trouve la carte qui porte l'adresse ZeroTier du PC et lit ses réglages.
    /// Renvoie null si aucune carte ne porte cette adresse.
    /// </summary>
    public static async Task<EtatCarteZeroTier?> LireAsync(string adresseIPv4)
    {
        // L'adresse vient de ZeroTier ; on vérifie quand même qu'elle est bien une IPv4
        // avant de l'insérer dans la commande.
        if (!IPAddress.TryParse(adresseIPv4, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ErreurZeroTier($"Adresse ZeroTier invalide : {adresseIPv4}");
        }

        var script =
            $"$a = Get-NetIPAddress -AddressFamily IPv4 -IPAddress '{ip}' -ErrorAction SilentlyContinue | Select-Object -First 1; "
            + "if (-not $a) { '{}'; exit 0 }; "
            + "$i = Get-NetIPInterface -InterfaceIndex $a.InterfaceIndex -AddressFamily IPv4; "
            + "$p = Get-NetConnectionProfile -InterfaceIndex $a.InterfaceIndex -ErrorAction SilentlyContinue | Select-Object -First 1; "
            + "$autres = @(Get-NetIPInterface -AddressFamily IPv4 -ConnectionState Connected "
            + "| Where-Object { $_.InterfaceIndex -ne $a.InterfaceIndex -and $_.InterfaceAlias -notlike 'Loopback*' } "
            + "| ForEach-Object { @{ alias = $_.InterfaceAlias; metrique = [int]$_.InterfaceMetric } }); "
            + "$cat = $null; if ($p) { $cat = $p.NetworkCategory.ToString() }; "
            + "@{ index = [int]$a.InterfaceIndex; alias = $a.InterfaceAlias; categorie = $cat; "
            + "metrique = [int]$i.InterfaceMetric; metriqueAuto = ($i.AutomaticMetric.ToString() -eq 'Enabled'); autres = $autres } "
            + "| ConvertTo-Json -Depth 4 -Compress";

        var resultat = await ExecuteurPowerShell.ExecuterAsync(script);
        if (resultat.Code != 0 || resultat.Sortie.Length == 0)
        {
            throw new ErreurZeroTier("Impossible de lire les réglages réseau de Windows. " + resultat.Erreurs);
        }
        try
        {
            return LecteurEtatCarte.Lire(resultat.Sortie);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ErreurZeroTier("Réponse de Windows incompréhensible : " + resultat.Sortie);
        }
    }

    /// <summary>Passe le réseau en « Privé » et rend la carte prioritaire. Renvoie null si tout va bien.</summary>
    public static async Task<string?> CorrigerAsync(int index)
    {
        var script =
            $"Set-NetConnectionProfile -InterfaceIndex {index} -NetworkCategory Private -ErrorAction Stop; "
            + $"Set-NetIPInterface -InterfaceIndex {index} -AddressFamily IPv4 -InterfaceMetric {DiagnosticCarte.MetriqueCible} -ErrorAction Stop";
        var resultat = await ExecuteurPowerShell.ExecuterAsync(script);
        return resultat.Reussi ? null : "La correction a échoué : " + PremiereLigne(resultat.Erreurs);
    }

    /// <summary>Rend à Windows le choix automatique de la priorité (annule la correction).</summary>
    public static async Task<string?> RetablirAsync(int index)
    {
        var resultat = await ExecuteurPowerShell.ExecuterAsync(
            $"Set-NetIPInterface -InterfaceIndex {index} -AddressFamily IPv4 -AutomaticMetric Enabled -ErrorAction Stop");
        return resultat.Reussi ? null : "Le rétablissement a échoué : " + PremiereLigne(resultat.Erreurs);
    }

    private static string PremiereLigne(string texte) =>
        texte.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "erreur inconnue";
}
