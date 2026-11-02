namespace PES6Connect.Core.ZeroTier;

public enum NiveauVerification
{
    Ok,
    Attention,
    Erreur,
}

public enum ActionProposee
{
    Aucune,
    Installer,
    DemarrerService,
    Rejoindre,
    CorrigerCarte,
    ChoisirPes6,
    DefinirReference,
}

public sealed record ResultatVerification(NiveauVerification Niveau, string Message, ActionProposee Action);

/// <summary>Transforme une observation technique en message clair + action à proposer.</summary>
public static class DiagnosticZeroTier
{
    public static ResultatVerification Evaluer(ObservationZeroTier obs)
    {
        var version = string.IsNullOrWhiteSpace(obs.Version) ? "" : $" (version {obs.Version})";

        return obs.Service switch
        {
            EtatService.Demarre => new(NiveauVerification.Ok,
                $"ZeroTier est installé{version} et fonctionne.", ActionProposee.Aucune),

            EtatService.Arrete => new(NiveauVerification.Attention,
                $"ZeroTier est installé{version} mais il est arrêté.", ActionProposee.DemarrerService),

            EtatService.EnTransition => new(NiveauVerification.Attention,
                "ZeroTier est en train de démarrer ou de s'arrêter. Revérifie dans quelques secondes.", ActionProposee.Aucune),

            EtatService.Absent when obs.CheminExecutable is not null => new(NiveauVerification.Erreur,
                "ZeroTier semble mal installé (son service Windows est absent). Réinstalle-le.", ActionProposee.Installer),

            EtatService.Absent => new(NiveauVerification.Erreur,
                "ZeroTier n'est pas installé sur ce PC.", ActionProposee.Installer),

            _ => new(NiveauVerification.Attention,
                "Impossible de vérifier ZeroTier. Relance PES6 Connect en acceptant les droits administrateur.", ActionProposee.Aucune),
        };
    }
}
