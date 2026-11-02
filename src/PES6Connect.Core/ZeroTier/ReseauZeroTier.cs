using System.Text.Json;

namespace PES6Connect.Core.ZeroTier;

/// <summary>Un réseau ZeroTier tel que le décrit le service local.</summary>
public sealed record ReseauZeroTier(
    string Id,
    string Nom,
    string Statut,
    IReadOnlyList<string> AdressesAttribuees,
    string? NomCarte)
{
    /// <summary>Première adresse IPv4 attribuée, sans le « /24 » final (ex. « 10.147.20.5 »).</summary>
    public string? AdresseIPv4 => AdressesAttribuees
        .Select(a => a.Split('/')[0])
        .FirstOrDefault(a => a.Contains('.'));

    /// <summary>Première adresse IPv4 avec son préfixe (ex. « 10.147.20.5/24 »).</summary>
    public string? AdresseIPv4AvecPrefixe => AdressesAttribuees.FirstOrDefault(a => a.Contains('.') && a.Contains('/'));
}

/// <summary>
/// Lit les réponses JSON de l'API locale de ZeroTier (http://127.0.0.1:9993).
/// Séparé de l'accès réseau pour pouvoir être testé sans ZeroTier.
/// </summary>
public static class LecteurApiZeroTier
{
    /// <summary>Identifiant du PC dans ZeroTier (10 caractères), réponse de GET /status.</summary>
    public static string? LireIdentifiantPc(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Texte(doc.RootElement, "address");
    }

    /// <summary>Version de ZeroTier, réponse de GET /status.</summary>
    public static string? LireVersion(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Texte(doc.RootElement, "version");
    }

    /// <summary>
    /// Réponse de GET /peer : identifiants des PC joints SANS chemin direct actif
    /// (le trafic passe alors par un relais ZeroTier, plus lent).
    /// </summary>
    public static IReadOnlySet<string> LirePairsRelayes(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var relayes = new HashSet<string>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return relayes;
        }
        foreach (var pair in doc.RootElement.EnumerateArray())
        {
            var id = Texte(pair, "address");
            if (id is null || Texte(pair, "role") is "PLANET" or "MOON")
            {
                continue;
            }
            var direct = pair.TryGetProperty("paths", out var chemins) && chemins.ValueKind == JsonValueKind.Array
                && chemins.EnumerateArray().Any(c =>
                    c.ValueKind == JsonValueKind.Object
                    && !(c.TryGetProperty("expired", out var exp) && exp.ValueKind == JsonValueKind.True)
                    && (!c.TryGetProperty("active", out var act) || act.ValueKind == JsonValueKind.True));
            if (!direct)
            {
                relayes.Add(id.ToLowerInvariant());
            }
        }
        return relayes;
    }

    /// <summary>Réponse de GET /network : liste des réseaux rejoints.</summary>
    public static IReadOnlyList<ReseauZeroTier> LireReseaux(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().Select(LireReseau).ToList()
            : [];
    }

    /// <summary>Réponse de GET ou POST /network/{id}.</summary>
    public static ReseauZeroTier LireReseau(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return LireReseau(doc.RootElement);
    }

    private static ReseauZeroTier LireReseau(JsonElement e)
    {
        var adresses = e.TryGetProperty("assignedAddresses", out var a) && a.ValueKind == JsonValueKind.Array
            ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
            : [];
        return new ReseauZeroTier(
            (Texte(e, "id") ?? Texte(e, "nwid") ?? "").ToLowerInvariant(),
            Texte(e, "name") ?? "",
            Texte(e, "status") ?? "",
            adresses,
            Texte(e, "portDeviceName"));
    }

    private static string? Texte(JsonElement e, string nom) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
