using PES6Connect.Core.ZeroTier;

namespace PES6Connect.Core.Presence;

/// <summary>Rédige le texte de la ligne « Joueurs en ligne ».</summary>
public static class AffichageJoueurs
{
    /// <param name="relayes">Identifiants des joueurs joints par un relais ZeroTier (connexion lente).</param>
    /// <param name="reference">Empreinte de référence de PES 6 (vide = pas de comparaison).</param>
    public static (NiveauVerification? Niveau, string Texte) Rediger(
        IReadOnlyList<JoueurEnLigne> joueurs,
        IReadOnlySet<string> relayes,
        string reference,
        SuggestionHote? suggestion)
    {
        if (joueurs.Count == 0)
        {
            return (null, "À l'écoute… Aucun autre joueur en ligne pour l'instant. La liste se met à jour toute seule.");
        }

        var lignes = new List<string>();
        var alerte = false;
        foreach (var j in joueurs)
        {
            var ligne = $"• {j.Nom} : " + (j.PingMs is { } ms ? $"{ms} ms" : "mesure en cours…");
            if (relayes.Contains(j.Id))
            {
                ligne += " · connexion relayée (lente)";
                alerte = true;
            }
            if (!MemeVersion(j, reference))
            {
                ligne += " · version de PES 6 différente !";
                alerte = true;
            }
            lignes.Add(ligne);
        }

        lignes.Add(suggestion switch
        {
            null => "Hôte conseillé : calcul en cours…",
            { AEgalite: true } s when joueurs.Count == 1 => $"Hôte conseillé : l'un ou l'autre, c'est pareil (ping {s.PirePingMs} ms).",
            { EstMoi: true } s => $"👉 Hôte conseillé : toi (ping max {s.PirePingMs} ms){(s.AEgalite ? ", à égalité avec un autre joueur" : "")}.",
            var s => $"👉 Hôte conseillé : {s.Nom} (ping max {s.PirePingMs} ms){(s.AEgalite ? ", à égalité avec un autre joueur" : "")}.",
        });

        return (alerte ? NiveauVerification.Attention : NiveauVerification.Ok, string.Join(Environment.NewLine, lignes));
    }

    public static bool MemeVersion(JoueurEnLigne joueur, string reference) =>
        string.IsNullOrEmpty(reference) || string.Equals(joueur.Empreinte, reference, StringComparison.OrdinalIgnoreCase);
}
