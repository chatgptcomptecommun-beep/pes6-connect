using Microsoft.Win32;

namespace PES6Connect.App;

/// <summary>Cherche PES6.exe sur le PC.</summary>
internal static class LocalisateurPes6
{
    public const string NomExe = "PES6.exe";

    /// <param name="cheminChoisi">Chemin choisi à la main et enregistré dans la configuration (prioritaire).</param>
    public static string? Trouver(string? cheminChoisi)
    {
        if (!string.IsNullOrWhiteSpace(cheminChoisi) && File.Exists(cheminChoisi))
        {
            return cheminChoisi;
        }
        return Candidats().FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> Candidats()
    {
        // 1. Dossier d'installation noté par l'installateur de Konami dans le registre.
        foreach (var cle in new[] { @"SOFTWARE\WOW6432Node\KONAMIPES6\PES6", @"SOFTWARE\KONAMIPES6\PES6" })
        {
            string? dossier = null;
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(cle);
                dossier = k?.GetValue("installdir") as string;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // On passe aux emplacements suivants.
            }
            if (!string.IsNullOrWhiteSpace(dossier))
            {
                yield return Path.Combine(dossier.Trim(), NomExe);
            }
        }

        // 2. Emplacements d'installation habituels.
        foreach (var racine in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                 })
        {
            if (string.IsNullOrEmpty(racine))
            {
                continue;
            }
            yield return Path.Combine(racine, "KONAMI", "Pro Evolution Soccer 6", NomExe);
            yield return Path.Combine(racine, "Pro Evolution Soccer 6", NomExe);
        }
    }
}
