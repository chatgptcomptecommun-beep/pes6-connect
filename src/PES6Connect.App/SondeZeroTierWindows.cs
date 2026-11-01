using System.Diagnostics;
using System.ServiceProcess;
using PES6Connect.Core.ZeroTier;

namespace PES6Connect.App;

/// <summary>Regarde sur le PC Windows si ZeroTier est installé et démarré.</summary>
internal sealed class SondeZeroTierWindows : ISondeZeroTier
{
    /// <summary>Nom interne du service installé par ZeroTier.</summary>
    public const string NomService = "ZeroTierOneService";

    private static readonly TimeSpan DelaiDemarrage = TimeSpan.FromSeconds(30);

    public static IEnumerable<string> EmplacementsPossibles()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Path.Combine(programData, "ZeroTier", "One", "zerotier-one_x64.exe");
        yield return Path.Combine(programData, "ZeroTier", "One", "zerotier-one_x86.exe");
        yield return Path.Combine(programFilesX86, "ZeroTier", "One", "zerotier_desktop_ui.exe");
    }

    public ObservationZeroTier Observer()
    {
        var exe = EmplacementsPossibles().FirstOrDefault(File.Exists);
        return new ObservationZeroTier(LireEtatService(), exe, LireVersion(exe));
    }

    /// <summary>Démarre le service. Renvoie null si tout va bien, sinon un message d'erreur.</summary>
    public string? DemarrerService()
    {
        try
        {
            using var service = new ServiceController(NomService);
            if (service.Status != ServiceControllerStatus.Running)
            {
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, DelaiDemarrage);
            }
            return null;
        }
        catch (System.ServiceProcess.TimeoutException)
        {
            return "ZeroTier met trop de temps à démarrer. Redémarre le PC puis réessaie.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return "Impossible de démarrer ZeroTier : " + ex.Message;
        }
    }

    private static EtatService LireEtatService()
    {
        try
        {
            using var service = new ServiceController(NomService);
            return service.Status switch
            {
                ServiceControllerStatus.Running => EtatService.Demarre,
                ServiceControllerStatus.Stopped or ServiceControllerStatus.Paused => EtatService.Arrete,
                _ => EtatService.EnTransition,
            };
        }
        catch (InvalidOperationException)
        {
            // Levée quand le service n'existe pas sur ce PC.
            return EtatService.Absent;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return EtatService.Inconnu;
        }
    }

    private static string? LireVersion(string? exe)
    {
        if (exe is null)
        {
            return null;
        }
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exe);
            var version = (info.ProductVersion ?? info.FileVersion)?.Trim();
            return string.IsNullOrEmpty(version) ? null : version;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
