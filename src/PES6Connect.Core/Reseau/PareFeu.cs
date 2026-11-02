using System.Text.Json;
using PES6Connect.Core.ZeroTier;

namespace PES6Connect.Core.Reseau;

/// <summary>
/// Ce que le pare-feu Windows prévoit pour PES6.exe.
/// </summary>
/// <param name="RegleExiste">La règle « PES6 Connect » existe.</param>
/// <param name="RegleActive">Elle est activée, entrante et autorise le trafic.</param>
/// <param name="Programme">Programme visé par la règle.</param>
/// <param name="Profils">Types de réseau concernés (ex. « Domain, Private »).</param>
/// <param name="Blocages">
/// Noms des règles actives qui BLOQUENT PES 6. Windows les crée quand on clique sur
/// « Annuler » à la question « Autoriser l'accès ? » au premier lancement du jeu.
/// Elles l'emportent sur toute autorisation.
/// </param>
/// <param name="RegleOutilOk">
/// La règle qui laisse PES6 Connect recevoir les annonces des autres joueurs (UDP 47106) est correcte.
/// </param>
public sealed record EtatPareFeu(
    bool RegleExiste,
    bool RegleActive,
    string? Programme,
    string? Profils,
    IReadOnlyList<string> Blocages,
    bool RegleOutilOk = true);

public static class PareFeu
{
    /// <summary>Nom interne (unique) de la règle créée par PES6 Connect.</summary>
    public const string NomRegle = "PES6Connect-PES6-Entrant";
    public const string NomAffiche = "PES 6 (PES6 Connect)";
    public const string Groupe = "PES6 Connect";

    /// <summary>Règle qui laisse PES6 Connect lui-même recevoir les autres joueurs (étape 6).</summary>
    public const string NomRegleOutil = "PES6Connect-Presence-Entrant";
    public const string NomAfficheOutil = "PES6 Connect (joueurs en ligne)";

    /// <summary>
    /// Met un chemin entre apostrophes pour PowerShell (une apostrophe s'écrit '' à l'intérieur).
    /// </summary>
    public static string ChaineCitee(string texte) => "'" + texte.Replace("'", "''") + "'";

    public static EtatPareFeu Lire(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var e = doc.RootElement;
        var blocages = new List<string>();
        if (e.TryGetProperty("blocages", out var b))
        {
            if (b.ValueKind == JsonValueKind.Array)
            {
                blocages.AddRange(b.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!));
            }
            else if (b.ValueKind == JsonValueKind.String)
            {
                blocages.Add(b.GetString()!);
            }
        }
        return new EtatPareFeu(
            Booleen(e, "regleExiste"),
            Booleen(e, "regleActive"),
            Texte(e, "programme"),
            Texte(e, "profils"),
            blocages,
            Booleen(e, "regleOutil"));
    }

    public static ResultatVerification Evaluer(EtatPareFeu etat, string cheminPes6)
    {
        if (etat.Blocages.Count > 0)
        {
            var noms = string.Join(", ", etat.Blocages.Distinct().Select(n => $"« {n} »"));
            return new(NiveauVerification.Attention,
                $"Le pare-feu Windows bloque PES 6 (règle {noms}). Clique sur « Corriger ».", ActionProposee.CorrigerPareFeu);
        }

        var bonProgramme = string.Equals(etat.Programme, cheminPes6, StringComparison.OrdinalIgnoreCase);
        var bonsProfils = etat.Profils is { } p && (p.Contains("Private") || p.Contains("Any"));

        if (!etat.RegleExiste)
        {
            return new(NiveauVerification.Attention,
                "Aucune règle du pare-feu Windows n'autorise PES 6 à recevoir les autres joueurs.", ActionProposee.CorrigerPareFeu);
        }
        if (!etat.RegleActive || !bonProgramme || !bonsProfils)
        {
            return new(NiveauVerification.Attention,
                "La règle du pare-feu pour PES 6 est incorrecte (désactivée ou pour un autre fichier).", ActionProposee.CorrigerPareFeu);
        }
        if (!etat.RegleOutilOk)
        {
            return new(NiveauVerification.Attention,
                "PES 6 est autorisé, mais PES6 Connect ne peut pas encore recevoir les autres joueurs (règle manquante).",
                ActionProposee.CorrigerPareFeu);
        }
        return new(NiveauVerification.Ok,
            "PES 6 et PES6 Connect sont autorisés par le pare-feu Windows sur les réseaux privés.", ActionProposee.Aucune);
    }

    private static bool Booleen(JsonElement e, string nom) =>
        e.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.True;

    private static string? Texte(JsonElement e, string nom) =>
        e.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
