namespace PES6Connect.Core.ZeroTier;

/// <summary>Explique en français où en est la connexion au réseau ZeroTier commun.</summary>
public static class DiagnosticReseau
{
    /// <summary>Statuts pour lesquels il faut attendre l'approbation de l'organisatrice.</summary>
    public static bool EstEnAttente(ReseauZeroTier? reseau) =>
        reseau?.Statut is "REQUESTING_CONFIGURATION" or "ACCESS_DENIED"
        || (reseau?.Statut == "OK" && reseau.AdresseIPv4 is null);

    /// <param name="reseau">Le réseau commun s'il a déjà été rejoint, sinon null.</param>
    /// <param name="identifiantPc">Identifiant ZeroTier de ce PC (à donner à l'organisatrice).</param>
    public static ResultatVerification Evaluer(ReseauZeroTier? reseau, string? identifiantPc)
    {
        var monId = string.IsNullOrEmpty(identifiantPc) ? "" : $" Ton identifiant ZeroTier : {identifiantPc}.";

        if (reseau is null)
        {
            return new(NiveauVerification.Attention,
                "Tu n'as pas encore rejoint notre réseau.", ActionProposee.Rejoindre);
        }

        var nom = string.IsNullOrWhiteSpace(reseau.Nom) ? "" : $" « {reseau.Nom} »";

        return reseau.Statut switch
        {
            "OK" when reseau.AdresseIPv4 is { } ip => new(NiveauVerification.Ok,
                $"Connecté au réseau{nom}. Ton adresse : {ip}", ActionProposee.Aucune),

            "OK" => new(NiveauVerification.Attention,
                $"Connecté au réseau{nom}, en attente d'une adresse… (l'organisatrice doit vérifier les réglages du réseau)", ActionProposee.Aucune),

            "REQUESTING_CONFIGURATION" or "ACCESS_DENIED" => new(NiveauVerification.Attention,
                $"Demande envoyée. L'organisatrice doit t'accepter sur my.zerotier.com.{monId} "
                + "Cette ligne passera au vert toute seule.", ActionProposee.Aucune),

            "NOT_FOUND" => new(NiveauVerification.Erreur,
                "Ce réseau n'existe pas. Vérifie « idReseauZeroTier » dans pes6connect.json.", ActionProposee.Aucune),

            "PORT_ERROR" => new(NiveauVerification.Erreur,
                "La carte réseau virtuelle de ZeroTier ne fonctionne pas. Redémarre le PC ; si ça continue, réinstalle ZeroTier.",
                ActionProposee.Aucune),

            "CLIENT_TOO_OLD" => new(NiveauVerification.Erreur,
                "Ta version de ZeroTier est trop ancienne : désinstalle-la puis réinstalle-la avec PES6 Connect.", ActionProposee.Aucune),

            "AUTHENTICATION_REQUIRED" => new(NiveauVerification.Erreur,
                "Ce réseau demande une connexion par compte, ce que PES6 Connect ne gère pas. Préviens l'organisatrice.",
                ActionProposee.Aucune),

            var autre => new(NiveauVerification.Attention,
                $"État du réseau inhabituel ({autre}). Revérifie dans quelques secondes.", ActionProposee.Aucune),
        };
    }
}
