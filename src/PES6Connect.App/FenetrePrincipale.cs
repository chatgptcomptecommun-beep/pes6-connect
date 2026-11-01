using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using PES6Connect.Core.Configuration;
using PES6Connect.Core.ZeroTier;

namespace PES6Connect.App;

internal sealed class FenetrePrincipale : Form
{
    private readonly string _cheminConfig;
    private readonly SondeZeroTierWindows _sonde;
    private readonly InstallateurZeroTier _installateur;

    private readonly LigneVerification _ligneConfig = new("Fichier de configuration");
    private readonly LigneVerification _ligneZeroTier = new("ZeroTier");
    private readonly LigneVerification[] _lignesAVenir =
    [
        new("Rejoindre notre réseau"),
        new("PES 6 (version identique)"),
        new("Pare-feu Windows"),
        new("Priorité de la carte ZeroTier"),
        new("Joueurs en ligne"),
    ];
    private readonly Button _boutonReverifier = new() { Text = "Tout revérifier", AutoSize = true, Padding = new Padding(8, 3, 8, 3) };
    private readonly Button _boutonRapport = new() { Text = "Copier le rapport", AutoSize = true, Padding = new Padding(8, 3, 8, 3) };

    private ObservationZeroTier? _derniereObservation;
    private ResultatInstallation? _derniereInstallation;

    public FenetrePrincipale(string cheminConfig, SondeZeroTierWindows sonde, InstallateurZeroTier installateur)
    {
        _cheminConfig = cheminConfig;
        _sonde = sonde;
        _installateur = installateur;

        Text = $"PES6 Connect {VersionApplication()}";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(640, 560);
        MinimumSize = new Size(560, 480);

        var liste = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12),
        };
        foreach (var ligne in new[] { _ligneConfig, _ligneZeroTier }.Concat(_lignesAVenir))
        {
            ligne.Width = 600;
            ligne.Dock = DockStyle.None;
            liste.Controls.Add(ligne);
        }
        foreach (var ligne in _lignesAVenir)
        {
            ligne.Definir(Statut.AVenir, "Bientôt disponible.");
        }

        var bas = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 12),
        };
        bas.Controls.Add(_boutonReverifier);
        bas.Controls.Add(_boutonRapport);

        Controls.Add(liste);
        Controls.Add(bas);

        _boutonReverifier.Click += async (_, _) => await VerifierToutAsync();
        _boutonRapport.Click += (_, _) => CopierRapport();
        Shown += async (_, _) => await VerifierToutAsync();
    }

    private async Task VerifierToutAsync()
    {
        _boutonReverifier.Enabled = false;
        try
        {
            VerifierConfiguration();
            await VerifierZeroTierAsync();
        }
        finally
        {
            _boutonReverifier.Enabled = true;
        }
    }

    // ---------- Étape 0 : fichier de configuration ----------

    private void VerifierConfiguration()
    {
        var resultat = ChargeurConfiguration.Charger(_cheminConfig);
        var problemes = string.Join(Environment.NewLine, resultat.Problemes);

        switch (resultat.Etat)
        {
            case EtatConfiguration.Valide:
                var c = resultat.Configuration!;
                _ligneConfig.Definir(Statut.Ok, $"Joueur : {c.NomJoueur} · Réseau : {c.IdReseauZeroTier}");
                _ligneConfig.DefinirAction(null);
                break;
            case EtatConfiguration.Illisible when !File.Exists(_cheminConfig):
                _ligneConfig.Definir(Statut.Erreur, problemes);
                _ligneConfig.DefinirAction(null);
                break;
            default:
                _ligneConfig.Definir(resultat.Etat == EtatConfiguration.Illisible ? Statut.Erreur : Statut.Attention, problemes);
                _ligneConfig.DefinirAction("Ouvrir le fichier", OuvrirConfigurationAsync);
                break;
        }
    }

    private Task OuvrirConfigurationAsync()
    {
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{_cheminConfig}\"") { UseShellExecute = true });
        return Task.CompletedTask;
    }

    // ---------- Étape 1 : ZeroTier ----------

    private async Task VerifierZeroTierAsync()
    {
        _ligneZeroTier.Definir(Statut.EnCours, "Vérification…");
        _ligneZeroTier.DefinirAction(null);

        var observation = await Task.Run(_sonde.Observer);
        AfficherZeroTier(observation);
    }

    private void AfficherZeroTier(ObservationZeroTier observation)
    {
        _derniereObservation = observation;
        var diagnostic = DiagnosticZeroTier.Evaluer(observation);

        var statut = diagnostic.Niveau switch
        {
            NiveauVerification.Ok => Statut.Ok,
            NiveauVerification.Attention => Statut.Attention,
            _ => Statut.Erreur,
        };
        _ligneZeroTier.Definir(statut, diagnostic.Message);

        switch (diagnostic.Action)
        {
            case ActionProposee.Installer:
                _ligneZeroTier.DefinirAction("Installer ZeroTier", InstallerZeroTierAsync);
                break;
            case ActionProposee.DemarrerService:
                _ligneZeroTier.DefinirAction("Démarrer ZeroTier", DemarrerZeroTierAsync);
                break;
            default:
                _ligneZeroTier.DefinirAction(null);
                break;
        }
    }

    private async Task DemarrerZeroTierAsync()
    {
        _ligneZeroTier.Definir(Statut.EnCours, "Démarrage de ZeroTier…");
        var erreur = await Task.Run(_sonde.DemarrerService);
        AfficherZeroTier(await Task.Run(_sonde.Observer));
        if (erreur is not null)
        {
            _ligneZeroTier.Definir(Statut.Erreur, erreur);
        }
    }

    private async Task InstallerZeroTierAsync()
    {
        var reponse = MessageBox.Show(this,
            "PES6 Connect va télécharger et installer ZeroTier depuis le catalogue officiel de Windows.\n\n"
            + "Cela prend en général 1 à 3 minutes. Continuer ?",
            "Installer ZeroTier", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (reponse != DialogResult.OK)
        {
            return;
        }

        _boutonReverifier.Enabled = false;
        _ligneZeroTier.Definir(Statut.EnCours, "Installation de ZeroTier en cours… (1 à 3 minutes, ne ferme pas la fenêtre)");
        _ligneZeroTier.DefinirAction(null);
        try
        {
            _derniereInstallation = await _installateur.InstallerAsync();

            // Après l'installation, le service peut mettre quelques secondes à démarrer.
            var observation = await AttendreServiceAsync(TimeSpan.FromSeconds(20));
            AfficherZeroTier(observation);

            if (observation.Service == EtatService.Absent)
            {
                var explication = _derniereInstallation.WingetDisponible
                    ? _derniereInstallation.Message
                    : "L'installation automatique n'est pas possible sur ce PC.";
                _ligneZeroTier.Definir(Statut.Erreur,
                    explication + Environment.NewLine
                    + "Clique sur « Télécharger », installe ZeroTier comme un programme normal, puis clique sur « Tout revérifier ».");
                _ligneZeroTier.DefinirAction("Télécharger", () =>
                {
                    InstallateurZeroTier.OuvrirPageTelechargement();
                    return Task.CompletedTask;
                });
            }
        }
        finally
        {
            _boutonReverifier.Enabled = true;
        }
    }

    private async Task<ObservationZeroTier> AttendreServiceAsync(TimeSpan delaiMax)
    {
        var chrono = Stopwatch.StartNew();
        while (true)
        {
            var observation = await Task.Run(_sonde.Observer);
            if (observation.Service == EtatService.Demarre || chrono.Elapsed >= delaiMax)
            {
                return observation;
            }
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    // ---------- Rapport pour le dépannage ----------

    private void CopierRapport()
    {
        var rapport = new StringBuilder();
        rapport.AppendLine($"PES6 Connect {VersionApplication()}");
        rapport.AppendLine($"Windows : {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        rapport.AppendLine($"Date : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        rapport.AppendLine();
        foreach (var ligne in new[] { _ligneConfig, _ligneZeroTier })
        {
            rapport.AppendLine($"[{ligne.Statut}] {ligne.Titre} : {ligne.Detail.Replace(Environment.NewLine, " / ")}");
        }
        if (_derniereObservation is { } o)
        {
            rapport.AppendLine($"ZeroTier : service={o.Service}, exe={o.CheminExecutable ?? "-"}, version={o.Version ?? "-"}");
        }
        if (_derniereInstallation is { } i)
        {
            rapport.AppendLine($"Installation : winget={(i.WingetDisponible ? "oui" : "non")}, code={i.CodeSortie?.ToString() ?? "-"}, {i.Message}");
            if (i.Journal.Length > 0)
            {
                rapport.AppendLine(i.Journal);
            }
        }

        Clipboard.SetText(rapport.ToString());
        MessageBox.Show(this, "Le rapport est copié. Colle-le (Ctrl+V) dans ton message.",
            "PES6 Connect", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string VersionApplication() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
}
