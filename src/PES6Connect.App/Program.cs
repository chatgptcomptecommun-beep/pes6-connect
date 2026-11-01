using PES6Connect.Core.Configuration;

namespace PES6Connect.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // En cas de plantage inattendu : un message clair plutôt qu'une fenêtre d'erreur technique.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => MessageBox.Show(
            "Une erreur inattendue s'est produite :\n\n" + e.Exception.Message
            + "\n\nFais une capture d'écran de ce message et envoie-la.",
            "PES6 Connect", MessageBoxButtons.OK, MessageBoxIcon.Error);

        // Le fichier de configuration est toujours cherché à côté de PES6Connect.exe.
        var cheminConfig = Path.Combine(AppContext.BaseDirectory, ChargeurConfiguration.NomFichier);
        Application.Run(new FenetrePrincipale(cheminConfig, new SondeZeroTierWindows(), new InstallateurZeroTier()));
    }
}
