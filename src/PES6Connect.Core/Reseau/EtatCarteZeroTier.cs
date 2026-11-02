using System.Text.Json;
using PES6Connect.Core.ZeroTier;

namespace PES6Connect.Core.Reseau;

/// <summary>Une autre carte réseau du PC (Wi-Fi, Ethernet…) et sa priorité.</summary>
public sealed record AutreCarte(string Nom, int Metrique);

/// <summary>
/// Réglages Windows de la carte réseau ZeroTier.
/// La « métrique » est la priorité de la carte : plus elle est petite, plus la carte
/// est prioritaire. PES 6 annonce ses parties LAN par la carte prioritaire.
/// </summary>
public sealed record EtatCarteZeroTier(
    int Index,
    string Nom,
    string? Categorie,
    int Metrique,
    bool MetriqueAutomatique,
    IReadOnlyList<AutreCarte> AutresCartes)
{
    public bool EstPrivee => Categorie is "Private" or "DomainAuthenticated";

    public AutreCarte? CarteLaPlusPrioritaire => AutresCartes.MinBy(c => c.Metrique);

    public bool EstPrioritaire => CarteLaPlusPrioritaire is not { } autre || Metrique < autre.Metrique;
}

public static class LecteurEtatCarte
{
    /// <summary>
    /// Lit le JSON produit par le script PowerShell de PES6 Connect.
    /// Renvoie null si la carte n'a pas été trouvée (objet vide).
    /// </summary>
    public static EtatCarteZeroTier? Lire(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var e = doc.RootElement;
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("index", out var index))
        {
            return null;
        }

        var autres = new List<AutreCarte>();
        if (e.TryGetProperty("autres", out var a))
        {
            // PowerShell écrit parfois un objet seul au lieu d'une liste d'un élément.
            var elements = a.ValueKind switch
            {
                JsonValueKind.Array => a.EnumerateArray().ToList(),
                JsonValueKind.Object => [a],
                _ => [],
            };
            autres.AddRange(elements
                .Where(x => x.TryGetProperty("metrique", out var m) && m.ValueKind == JsonValueKind.Number)
                .Select(x => new AutreCarte(Texte(x, "alias") ?? "?", x.GetProperty("metrique").GetInt32())));
        }

        return new EtatCarteZeroTier(
            index.GetInt32(),
            Texte(e, "alias") ?? "ZeroTier",
            Texte(e, "categorie"),
            e.TryGetProperty("metrique", out var met) && met.ValueKind == JsonValueKind.Number ? met.GetInt32() : int.MaxValue,
            e.TryGetProperty("metriqueAuto", out var auto) && auto.ValueKind == JsonValueKind.True,
            autres);
    }

    private static string? Texte(JsonElement e, string nom) =>
        e.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

public static class DiagnosticCarte
{
    /// <summary>Métrique donnée à la carte ZeroTier (la plus petite possible en pratique).</summary>
    public const int MetriqueCible = 1;

    public static ResultatVerification Evaluer(EtatCarteZeroTier? carte)
    {
        if (carte is null)
        {
            return new(NiveauVerification.Erreur,
                "La carte réseau ZeroTier est introuvable dans Windows. Redémarre le PC puis revérifie.", ActionProposee.Aucune);
        }

        var problemes = new List<string>();
        if (!carte.EstPrivee)
        {
            problemes.Add("Windows considère le réseau ZeroTier comme « Public » (il bloque les parties LAN).");
        }
        if (!carte.EstPrioritaire)
        {
            problemes.Add($"La carte ZeroTier n'est pas prioritaire (c'est « {carte.CarteLaPlusPrioritaire!.Nom} »).");
        }

        if (problemes.Count == 0)
        {
            return new(NiveauVerification.Ok,
                "Réseau ZeroTier en « Privé » et prioritaire : PES 6 verra les parties LAN.", ActionProposee.Aucune);
        }

        // Déjà au minimum et toujours à égalité : on ne peut rien faire de plus automatiquement.
        if (carte.EstBloqueParEgalite())
        {
            return new(NiveauVerification.Attention,
                $"La carte « {carte.CarteLaPlusPrioritaire!.Nom} » a la même priorité que ZeroTier. "
                + "Si les parties LAN n'apparaissent pas, désactive-la pendant les matchs.", ActionProposee.Aucune);
        }

        return new(NiveauVerification.Attention, string.Join(" ", problemes), ActionProposee.CorrigerCarte);
    }

    private static bool EstBloqueParEgalite(this EtatCarteZeroTier carte) =>
        carte.EstPrivee
        && carte.Metrique <= MetriqueCible
        && carte.CarteLaPlusPrioritaire is { } autre
        && autre.Metrique <= carte.Metrique;
}
