using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PES6Connect.App;

internal sealed record ResultatPowerShell(int Code, string Sortie, string Erreurs)
{
    public bool Reussi => Code == 0 && Erreurs.Length == 0;
}

/// <summary>
/// Lance de courtes commandes PowerShell (outil d'administration intégré à Windows)
/// pour lire ou modifier les réglages réseau.
/// </summary>
internal static class ExecuteurPowerShell
{
    private static readonly TimeSpan Delai = TimeSpan.FromSeconds(30);

    /// <param name="script">Script sur une seule ligne, sans guillemets doubles.</param>
    public static async Task<ResultatPowerShell> ExecuterAsync(string script)
    {
        var infos = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        infos.ArgumentList.Add("-NoProfile");
        infos.ArgumentList.Add("-NonInteractive");
        infos.ArgumentList.Add("-Command");
        // Sortie en UTF-8 pour que les accents des noms de cartes soient bien lus.
        infos.ArgumentList.Add("[Console]::OutputEncoding = [Text.Encoding]::UTF8; $ProgressPreference = 'SilentlyContinue'; " + script);

        Process processus;
        try
        {
            processus = Process.Start(infos) ?? throw new Win32Exception();
        }
        catch (Win32Exception)
        {
            return new(-1, "", "PowerShell est introuvable sur ce PC.");
        }

        using (processus)
        {
            var sortie = processus.StandardOutput.ReadToEndAsync();
            var erreurs = processus.StandardError.ReadToEndAsync();
            using var delai = new CancellationTokenSource(Delai);
            try
            {
                await processus.WaitForExitAsync(delai.Token);
            }
            catch (OperationCanceledException)
            {
                processus.Kill(entireProcessTree: true);
                return new(-1, "", "La commande Windows a pris trop de temps.");
            }
            return new(processus.ExitCode, (await sortie).Trim(), (await erreurs).Trim());
        }
    }
}
