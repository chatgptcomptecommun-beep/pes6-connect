using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using PES6Connect.Core.Configuration;
using PES6Connect.Core.Pes6;
using PES6Connect.Core.Reseau;
using PES6Connect.Core.ZeroTier;

namespace PES6Connect.App;

internal sealed class FenetrePrincipale : Form
{
    private readonly string _cheminConfig;
    private readonly SondeZeroTierWindows _sonde;
    private readonly InstallateurZeroTier _installateur;
    private readonly ClientApiZeroTier _api = new();

    private readonly LigneVerification _ligneConfig = new("Fichier de configuration");
    private readonly LigneVerification _ligneZeroTier = new("ZeroTier");
    private readonly LigneVerification _ligneReseau = new("Notre réseau");
    private readonly LigneVerification _ligneCarte = new("Réglages réseau de Windows");
    private readonly LigneVerification _lignePes6 = new("PES 6 (version identique)");
    private readonly LigneVerification _lignePareFeu = new("Pare-feu Windows");
    private readonly LigneVerification[] _lignesAVenir =
    [
        new("Joueurs en ligne"),
    ];
    private readonly Button _boutonReverifier = new() { Text = "Tout revérifier", AutoSize = true, Padding = new Padding(8, 3, 8, 3) };
    private readonly Button _boutonRapport = new() { Text = "Copier le rapport", AutoSize = true, Padding = new Padding(8, 3, 8, 3) };

    // Pendant l'attente d'approbation, l'état du réseau est revérifié toutes les 10 secondes.
    private readonly System.Windows.Forms.Timer _minuterieAttente = new() { Interval = 10_000 };

    private ObservationZeroTier? _derniereObservation;
    private ResultatInstallation? _derniereInstallation;
    private string? _idReseau;
    private string? _identifiantPc;
    private string? _versionApi;
    private ReseauZeroTier? _dernierReseau;
    private bool _verificationReseauEnCours;
    private ConfigurationPes6Connect? _config;
    private EtatCarteZeroTier? _derniereCarte;
    private string? _cheminPes6;
    private string? _empreintePes6;
    private EtatPareFeu? _dernierPareFeu;

    public FenetrePrincipale(string cheminConfig, SondeZeroTierWindows sonde, InstallateurZeroTier installateur)
    {
        _cheminConfig = cheminConfig;
        _sonde = sonde;
        _installateur = installateur;

        Text = $"PES6 Connect {VersionApplication()}";
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(640, 660);
        MinimumSize = new Size(560, 480);

        var liste = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12),
        };
        foreach (var ligne in LignesActives().Concat(_lignesAVenir))
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
        _minuterieAttente.Tick += async (_, _) => await VerifierReseauAsync(silencieux: true);
        FormClosed += (_, _) =>
        {
            _minuterieAttente.Dispose();
            _api.Dispose();
        };
    }

    private LigneVerification[] LignesActives() => [_ligneConfig, _ligneZeroTier, _ligneReseau, _ligneCarte, _lignePes6, _lignePareFeu];

    private static Statut VersStatut(NiveauVerification niveau) => niveau switch
    {
        NiveauVerification.Ok => Statut.Ok,
        NiveauVerification.Attention => Statut.Attention,
        _ => Statut.Erreur,
    };

    private async Task VerifierToutAsync()
    {
        _boutonReverifier.Enabled = false;
        try
        {
            VerifierConfiguration();
            await VerifierZeroTierAsync();
            await VerifierReseauAsync();
            await VerifierPes6Async();
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
        _config = resultat.Configuration;
        var id = resultat.Configuration?.IdReseauZeroTier ?? "";
        _idReseau = ChargeurConfiguration.EstIdReseauValide(id) ? id : null;
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
        await VerifierReseauAsync();
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
            await VerifierReseauAsync();
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

    // ---------- Étape 2 : rejoindre notre réseau ----------

    private async Task VerifierReseauAsync(bool silencieux = false)
    {
        if (_verificationReseauEnCours)
        {
            return;
        }
        if (_idReseau is null)
        {
            ArreterAttente();
            _ligneReseau.Definir(Statut.AVenir, "En attente : l'identifiant du réseau doit d'abord être correct dans le fichier de configuration.");
            _ligneReseau.DefinirAction(null);
            return;
        }
        if (_derniereObservation?.Service != EtatService.Demarre)
        {
            ArreterAttente();
            _ligneReseau.Definir(Statut.AVenir, "En attente : ZeroTier doit d'abord fonctionner.");
            _ligneReseau.DefinirAction(null);
            return;
        }

        _verificationReseauEnCours = true;
        if (!silencieux)
        {
            _ligneReseau.Definir(Statut.EnCours, "Vérification…");
            _ligneReseau.DefinirAction(null);
        }
        try
        {
            await LireStatutApiAsync();
            AfficherReseau(await _api.LireReseauAsync(_idReseau));
            await VerifierCarteAsync();
        }
        catch (ErreurZeroTier ex)
        {
            ArreterAttente();
            _ligneReseau.Definir(Statut.Erreur, ex.Message);
            _ligneReseau.DefinirAction(null);
        }
        finally
        {
            _verificationReseauEnCours = false;
        }
    }

    private void AfficherReseau(ReseauZeroTier? reseau)
    {
        _dernierReseau = reseau;
        var diagnostic = DiagnosticReseau.Evaluer(reseau, _identifiantPc);
        _ligneReseau.Definir(VersStatut(diagnostic.Niveau), diagnostic.Message);

        if (diagnostic.Action == ActionProposee.Rejoindre)
        {
            _ligneReseau.DefinirAction("Rejoindre", RejoindreReseauAsync);
        }
        else if (DiagnosticReseau.EstEnAttente(reseau) && _identifiantPc is not null)
        {
            _ligneReseau.DefinirAction("Copier mon identifiant", CopierIdentifiantAsync);
        }
        else
        {
            _ligneReseau.DefinirAction(null);
        }

        if (DiagnosticReseau.EstEnAttente(reseau))
        {
            _minuterieAttente.Start();
        }
        else
        {
            ArreterAttente();
        }
    }

    private async Task RejoindreReseauAsync()
    {
        if (_idReseau is null)
        {
            return;
        }
        _verificationReseauEnCours = true;
        _ligneReseau.Definir(Statut.EnCours, "Connexion au réseau…");
        _ligneReseau.DefinirAction(null);
        try
        {
            await LireStatutApiAsync();
            var reseau = await _api.RejoindreAsync(_idReseau);

            // ZeroTier met quelques secondes à obtenir sa réponse : on patiente jusqu'à 15 s.
            var chrono = System.Diagnostics.Stopwatch.StartNew();
            while (reseau.Statut is "REQUESTING_CONFIGURATION" or "" && chrono.Elapsed < TimeSpan.FromSeconds(15))
            {
                await Task.Delay(1500);
                reseau = await _api.LireReseauAsync(_idReseau) ?? reseau;
            }
            AfficherReseau(reseau);
        }
        catch (ErreurZeroTier ex)
        {
            _ligneReseau.Definir(Statut.Erreur, ex.Message);
            _ligneReseau.DefinirAction("Réessayer", RejoindreReseauAsync);
        }
        finally
        {
            _verificationReseauEnCours = false;
        }
        await VerifierCarteAsync();
    }

    /// <summary>Lit l'identifiant du PC et, si besoin, complète la version de ZeroTier affichée.</summary>
    private async Task LireStatutApiAsync()
    {
        if (_identifiantPc is null)
        {
            (_identifiantPc, _versionApi) = await _api.LireStatutAsync();
        }
        // La version lue dans le fichier est souvent absente : on la complète à chaque vérification.
        if (_derniereObservation is { Version: null } obs && _versionApi is not null)
        {
            AfficherZeroTier(obs with { Version = _versionApi });
        }
    }

    private Task CopierIdentifiantAsync()
    {
        if (_identifiantPc is not null)
        {
            Clipboard.SetText(_identifiantPc);
            MessageBox.Show(this,
                $"Ton identifiant ZeroTier ({_identifiantPc}) est copié.\n\nEnvoie-le à l'organisatrice pour qu'elle t'accepte.",
                "PES6 Connect", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        return Task.CompletedTask;
    }

    private void ArreterAttente() => _minuterieAttente.Stop();

    // ---------- Étape 3 : réseau « Privé » et carte ZeroTier prioritaire ----------

    private async Task VerifierCarteAsync()
    {
        var ip = _dernierReseau is { Statut: "OK" } r ? r.AdresseIPv4 : null;
        if (ip is null || _derniereObservation?.Service != EtatService.Demarre)
        {
            _derniereCarte = null;
            _ligneCarte.Definir(Statut.AVenir, "En attente : il faut d'abord être connecté à notre réseau.");
            _ligneCarte.DefinirAction(null);
            return;
        }

        _ligneCarte.Definir(Statut.EnCours, "Vérification…");
        _ligneCarte.DefinirAction(null);
        try
        {
            AfficherCarte(await CarteZeroTierWindows.LireAsync(ip));
        }
        catch (ErreurZeroTier ex)
        {
            _ligneCarte.Definir(Statut.Erreur, ex.Message);
            _ligneCarte.DefinirAction("Réessayer", VerifierCarteAsync);
        }
    }

    private void AfficherCarte(EtatCarteZeroTier? carte)
    {
        _derniereCarte = carte;
        var diagnostic = DiagnosticCarte.Evaluer(carte);
        _ligneCarte.Definir(VersStatut(diagnostic.Niveau), diagnostic.Message);

        if (diagnostic.Action == ActionProposee.CorrigerCarte)
        {
            _ligneCarte.DefinirAction("Corriger", CorrigerCarteAsync);
        }
        else if (carte is { MetriqueAutomatique: false } && diagnostic.Niveau == NiveauVerification.Ok)
        {
            _ligneCarte.DefinirAction("Annuler", RetablirCarteAsync);
        }
        else
        {
            _ligneCarte.DefinirAction(null);
        }
    }

    private async Task CorrigerCarteAsync()
    {
        if (_derniereCarte is not { } carte)
        {
            return;
        }
        _ligneCarte.Definir(Statut.EnCours, "Correction des réglages réseau…");
        var erreur = await CarteZeroTierWindows.CorrigerAsync(carte.Index);
        await VerifierCarteAsync();
        if (erreur is not null && _ligneCarte.Statut != Statut.Ok)
        {
            _ligneCarte.Definir(Statut.Erreur, erreur);
            _ligneCarte.DefinirAction("Réessayer", CorrigerCarteAsync);
        }
    }

    private async Task RetablirCarteAsync()
    {
        if (_derniereCarte is not { } carte)
        {
            return;
        }
        var reponse = MessageBox.Show(this,
            "Rendre à Windows le choix automatique de la priorité des cartes réseau ?\n\n"
            + "PES 6 risque de ne plus voir les parties LAN. Tu pourras recliquer sur « Corriger » ensuite.",
            "Annuler la priorité", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (reponse != DialogResult.OK)
        {
            return;
        }
        var erreur = await CarteZeroTierWindows.RetablirAsync(carte.Index);
        await VerifierCarteAsync();
        if (erreur is not null)
        {
            _ligneCarte.Definir(Statut.Erreur, erreur);
        }
    }

    // ---------- Étape 4 : PES 6 et empreinte ----------

    private async Task VerifierPes6Async()
    {
        _lignePes6.Definir(Statut.EnCours, "Recherche de PES 6…");
        _lignePes6.DefinirAction(null);

        var cheminChoisi = _config?.CheminPes6;
        (_cheminPes6, _empreintePes6) = await Task.Run(() =>
        {
            var chemin = LocalisateurPes6.Trouver(cheminChoisi);
            if (chemin is null)
            {
                return ((string?)null, (string?)null);
            }
            try
            {
                return (chemin, EmpreinteFichier.CalculerSha256(chemin));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return (chemin, null);
            }
        });

        var diagnostic = DiagnosticPes6.Evaluer(_cheminPes6, _empreintePes6, _config?.EmpreintePes6 ?? "");
        _lignePes6.Definir(VersStatut(diagnostic.Niveau), diagnostic.Message);
        switch (diagnostic.Action)
        {
            case ActionProposee.ChoisirPes6:
                _lignePes6.DefinirAction("Choisir PES6.exe", ChoisirPes6Async);
                break;
            case ActionProposee.DefinirReference:
                _lignePes6.DefinirAction("En faire la référence", DefinirReferenceAsync);
                break;
            default:
                _lignePes6.DefinirAction(null);
                break;
        }

        await VerifierPareFeuAsync();
    }

    // ---------- Étape 5 : pare-feu Windows ----------

    private async Task VerifierPareFeuAsync()
    {
        if (_cheminPes6 is null)
        {
            _dernierPareFeu = null;
            _lignePareFeu.Definir(Statut.AVenir, "En attente : PES 6 doit d'abord être trouvé.");
            _lignePareFeu.DefinirAction(null);
            return;
        }

        _lignePareFeu.Definir(Statut.EnCours, "Vérification du pare-feu…");
        _lignePareFeu.DefinirAction(null);
        try
        {
            _dernierPareFeu = await PareFeuWindows.LireAsync(_cheminPes6);
        }
        catch (ErreurZeroTier ex)
        {
            _lignePareFeu.Definir(Statut.Erreur, ex.Message);
            _lignePareFeu.DefinirAction("Réessayer", VerifierPareFeuAsync);
            return;
        }

        var diagnostic = PareFeu.Evaluer(_dernierPareFeu, _cheminPes6);
        _lignePareFeu.Definir(VersStatut(diagnostic.Niveau), diagnostic.Message);
        if (diagnostic.Action == ActionProposee.CorrigerPareFeu)
        {
            _lignePareFeu.DefinirAction("Corriger", CorrigerPareFeuAsync);
        }
        else if (diagnostic.Niveau == NiveauVerification.Ok)
        {
            _lignePareFeu.DefinirAction("Annuler", SupprimerRegleAsync);
        }
        else
        {
            _lignePareFeu.DefinirAction(null);
        }
    }

    private async Task CorrigerPareFeuAsync()
    {
        if (_cheminPes6 is null)
        {
            return;
        }
        _lignePareFeu.Definir(Statut.EnCours, "Réglage du pare-feu…");
        var erreur = await PareFeuWindows.CorrigerAsync(_cheminPes6);
        await VerifierPareFeuAsync();
        if (erreur is not null && _lignePareFeu.Statut != Statut.Ok)
        {
            _lignePareFeu.Definir(Statut.Erreur, erreur);
            _lignePareFeu.DefinirAction("Réessayer", CorrigerPareFeuAsync);
        }
    }

    private async Task SupprimerRegleAsync()
    {
        var reponse = MessageBox.Show(this,
            "Supprimer la règle du pare-feu qui autorise PES 6 ?\n\n"
            + "Les autres joueurs ne pourront plus rejoindre tes parties. Tu pourras recliquer sur « Corriger » ensuite.",
            "Pare-feu", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (reponse != DialogResult.OK)
        {
            return;
        }
        var erreur = await PareFeuWindows.SupprimerAsync();
        await VerifierPareFeuAsync();
        if (erreur is not null)
        {
            _lignePareFeu.Definir(Statut.Erreur, erreur);
        }
    }

    private async Task ChoisirPes6Async()
    {
        using var dialogue = new OpenFileDialog
        {
            Title = "Où se trouve PES6.exe ?",
            Filter = "Pro Evolution Soccer 6 (PES6.exe)|PES6.exe|Programmes (*.exe)|*.exe",
            CheckFileExists = true,
        };
        if (dialogue.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        var erreur = ChargeurConfiguration.Modifier(_cheminConfig, c => c.CheminPes6 = dialogue.FileName);
        if (erreur is not null)
        {
            MessageBox.Show(this, erreur, "PES6 Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        VerifierConfiguration();
        await VerifierPes6Async();
    }

    private async Task DefinirReferenceAsync()
    {
        if (_empreintePes6 is not { } empreinte)
        {
            return;
        }
        var reponse = MessageBox.Show(this,
            "Ce PC va servir de référence : tous les joueurs devront avoir exactement le même PES6.exe.\n\n"
            + $"Fichier : {_cheminPes6}\n\nContinuer ?",
            "Empreinte de référence", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (reponse != DialogResult.OK)
        {
            return;
        }
        var erreur = ChargeurConfiguration.Modifier(_cheminConfig, c => c.EmpreintePes6 = empreinte);
        if (erreur is not null)
        {
            MessageBox.Show(this, erreur, "PES6 Connect", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Clipboard.SetText(empreinte);
        MessageBox.Show(this,
            $"Empreinte enregistrée et copiée :\n\n{empreinte}\n\n"
            + "Colle-la (Ctrl+V) dans ton message pour qu'elle soit ajoutée au fichier de configuration distribué à tous.",
            "PES6 Connect", MessageBoxButtons.OK, MessageBoxIcon.Information);
        VerifierConfiguration();
        await VerifierPes6Async();
    }

    // ---------- Rapport pour le dépannage ----------

    private void CopierRapport()
    {
        var rapport = new StringBuilder();
        rapport.AppendLine($"PES6 Connect {VersionApplication()}");
        rapport.AppendLine($"Windows : {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        rapport.AppendLine($"Date : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        rapport.AppendLine();
        foreach (var ligne in LignesActives())
        {
            rapport.AppendLine($"[{ligne.Statut}] {ligne.Titre} : {ligne.Detail.Replace(Environment.NewLine, " / ")}");
        }
        if (_derniereObservation is { } o)
        {
            rapport.AppendLine($"ZeroTier : service={o.Service}, exe={o.CheminExecutable ?? "-"}, version={o.Version ?? "-"}");
        }
        rapport.AppendLine($"Identifiant ZeroTier du PC : {_identifiantPc ?? "-"}");
        if (_dernierReseau is { } r)
        {
            rapport.AppendLine($"Réseau : id={r.Id}, statut={r.Statut}, adresses={string.Join(" ", r.AdressesAttribuees)}, carte={r.NomCarte ?? "-"}");
        }
        if (_derniereCarte is { } carte)
        {
            var autres = string.Join(", ", carte.AutresCartes.Select(a => $"{a.Nom}={a.Metrique}"));
            rapport.AppendLine($"Carte : index={carte.Index}, nom={carte.Nom}, catégorie={carte.Categorie ?? "-"}, "
                + $"métrique={carte.Metrique} (auto={carte.MetriqueAutomatique}), autres : {autres}");
        }
        rapport.AppendLine($"PES 6 : {_cheminPes6 ?? "introuvable"}");
        rapport.AppendLine($"Empreinte PES 6 : {_empreintePes6 ?? "-"}");
        if (_dernierPareFeu is { } pf)
        {
            rapport.AppendLine($"Pare-feu : règle={pf.RegleExiste}, active={pf.RegleActive}, profils={pf.Profils ?? "-"}, "
                + $"programme={pf.Programme ?? "-"}, blocages={(pf.Blocages.Count == 0 ? "aucun" : string.Join(" | ", pf.Blocages))}");
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
