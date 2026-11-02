using PES6Connect.Core.Presence;
using PES6Connect.Core.Reseau;

namespace PES6Connect.App;

/// <summary>Lit et modifie le pare-feu Windows pour PES6.exe (via PowerShell).</summary>
internal static class PareFeuWindows
{
    /// <summary>Chemin de PES6Connect.exe (la règle « joueurs en ligne » le vise).</summary>
    private static string CheminOutil => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "PES6Connect.exe");

    // Règles actives, entrantes, qui bloquent ce programme (comparaison sans tenir compte des majuscules).
    private static string ScriptBlocages(string chemin) =>
        $"$p = {PareFeu.ChaineCitee(chemin)}; "
        + "$blocs = @(Get-NetFirewallRule -Direction Inbound -Action Block -Enabled True -ErrorAction SilentlyContinue "
        + "| Where-Object { ($_ | Get-NetFirewallApplicationFilter).Program -ieq $p }); ";

    public static async Task<EtatPareFeu> LireAsync(string chemin)
    {
        var script = ScriptBlocages(chemin)
            + $"$r = Get-NetFirewallRule -Name '{PareFeu.NomRegle}' -ErrorAction SilentlyContinue; "
            + "$ok = $false; $prog = $null; $prof = $null; "
            + "if ($r) { $prog = ($r | Get-NetFirewallApplicationFilter).Program; $prof = $r.Profile.ToString(); "
            + "$ok = ($r.Enabled.ToString() -eq 'True') -and ($r.Action.ToString() -eq 'Allow') -and ($r.Direction.ToString() -eq 'Inbound') }; "
            + $"$o = {PareFeu.ChaineCitee(CheminOutil)}; "
            + $"$r2 = Get-NetFirewallRule -Name '{PareFeu.NomRegleOutil}' -ErrorAction SilentlyContinue; $ok2 = $false; "
            + "if ($r2) { $ok2 = ($r2.Enabled.ToString() -eq 'True') -and ($r2.Action.ToString() -eq 'Allow') "
            + "-and (($r2 | Get-NetFirewallApplicationFilter).Program -ieq $o) "
            + $"-and ((($r2 | Get-NetFirewallPortFilter).LocalPort) -contains '{MessagePresence.Port}') }}; "
            + "@{ regleExiste = [bool]$r; regleActive = $ok; programme = $prog; profils = $prof; regleOutil = $ok2; "
            + "blocages = @($blocs | ForEach-Object { $_.DisplayName }) } | ConvertTo-Json -Compress";

        var resultat = await ExecuteurPowerShell.ExecuterAsync(script);
        if (resultat.Code != 0 || resultat.Sortie.Length == 0)
        {
            throw new ErreurZeroTier("Impossible de lire le pare-feu Windows. " + resultat.Erreurs);
        }
        try
        {
            return PareFeu.Lire(resultat.Sortie);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ErreurZeroTier("Réponse du pare-feu incompréhensible : " + resultat.Sortie);
        }
    }

    /// <summary>
    /// (Re)crée la règle qui autorise PES 6 et désactive (sans les supprimer) les règles qui le bloquent.
    /// Renvoie null si tout va bien.
    /// </summary>
    public static async Task<string?> CorrigerAsync(string chemin)
    {
        var script = ScriptBlocages(chemin)
            + "$blocs | Disable-NetFirewallRule -ErrorAction Stop; "
            + $"Remove-NetFirewallRule -Name '{PareFeu.NomRegle}' -ErrorAction SilentlyContinue; "
            + $"New-NetFirewallRule -Name '{PareFeu.NomRegle}' -DisplayName '{PareFeu.NomAffiche}' -Group '{PareFeu.Groupe}' "
            + $"-Direction Inbound -Action Allow -Program {PareFeu.ChaineCitee(chemin)} -Profile Private,Domain -ErrorAction Stop | Out-Null; "
            + $"Remove-NetFirewallRule -Name '{PareFeu.NomRegleOutil}' -ErrorAction SilentlyContinue; "
            + $"New-NetFirewallRule -Name '{PareFeu.NomRegleOutil}' -DisplayName '{PareFeu.NomAfficheOutil}' -Group '{PareFeu.Groupe}' "
            + $"-Direction Inbound -Action Allow -Program {PareFeu.ChaineCitee(CheminOutil)} -Protocol UDP -LocalPort {MessagePresence.Port} "
            + "-Profile Private,Domain -ErrorAction Stop | Out-Null";
        var resultat = await ExecuteurPowerShell.ExecuterAsync(script);
        return resultat.Reussi ? null : "La correction du pare-feu a échoué : " + PremiereLigne(resultat.Erreurs);
    }

    /// <summary>Supprime les règles créées par PES6 Connect. Renvoie null si tout va bien.</summary>
    public static async Task<string?> SupprimerAsync()
    {
        var resultat = await ExecuteurPowerShell.ExecuterAsync(
            $"Remove-NetFirewallRule -Name '{PareFeu.NomRegle}' -ErrorAction Stop; "
            + $"Remove-NetFirewallRule -Name '{PareFeu.NomRegleOutil}' -ErrorAction SilentlyContinue");
        return resultat.Reussi ? null : "La suppression a échoué : " + PremiereLigne(resultat.Erreurs);
    }

    private static string PremiereLigne(string texte) =>
        texte.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "erreur inconnue";
}
