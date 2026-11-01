using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using PES6Connect.Core.ZeroTier;

namespace PES6Connect.App;

internal sealed record ResultatInstallation(bool WingetDisponible, int? CodeSortie, string Message, string Journal);

/// <summary>Installe ZeroTier avec winget, l'installateur intégré à Windows 10/11.</summary>
internal sealed class InstallateurZeroTier
{
    public async Task<ResultatInstallation> InstallerAsync()
    {
        var infos = new ProcessStartInfo("winget")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in PlanInstallationZeroTier.ArgumentsWinget())
        {
            infos.ArgumentList.Add(argument);
        }

        Process processus;
        try
        {
            processus = Process.Start(infos) ?? throw new Win32Exception();
        }
        catch (Win32Exception)
        {
            return new(false, null,
                "L'installateur automatique de Windows (winget) n'est pas disponible sur ce PC.", "");
        }

        using (processus)
        {
            var sortie = processus.StandardOutput.ReadToEndAsync();
            var erreurs = processus.StandardError.ReadToEndAsync();
            using var delai = new CancellationTokenSource(PlanInstallationZeroTier.DelaiMaxInstallation);
            try
            {
                await processus.WaitForExitAsync(delai.Token);
            }
            catch (OperationCanceledException)
            {
                processus.Kill(entireProcessTree: true);
                return new(true, null, "L'installation a pris trop de temps et a été arrêtée.", "");
            }

            var journal = Nettoyer(await sortie + "\n" + await erreurs);
            return new(true, processus.ExitCode, PlanInstallationZeroTier.ExpliquerCodeSortie(processus.ExitCode), journal);
        }
    }

    public static void OuvrirPageTelechargement() =>
        Process.Start(new ProcessStartInfo(PlanInstallationZeroTier.PageTelechargement) { UseShellExecute = true });

    /// <summary>Garde seulement les dernières lignes utiles (winget affiche des barres de progression).</summary>
    private static string Nettoyer(string texte)
    {
        var lignes = texte.Split('\n', '\r')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && l.Any(char.IsLetter))
            .TakeLast(15);
        return string.Join(Environment.NewLine, lignes);
    }
}
