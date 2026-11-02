using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PES6Connect.Core.Presence;

public enum TypeMessage
{
    /// <summary>« Je suis là » : envoyé à tout le réseau toutes les 3 secondes.</summary>
    Annonce,
    /// <summary>Demande de réponse immédiate, pour mesurer le ping.</summary>
    Ping,
    /// <summary>Réponse à un ping.</summary>
    Pong,
}

/// <summary>
/// Petit message échangé entre les PES6 Connect ouverts sur le réseau ZeroTier.
/// Les messages reçus viennent d'autres PC : ils sont vérifiés strictement avant usage.
/// </summary>
public sealed record MessagePresence(
    TypeMessage Type,
    string Id,
    string Nom,
    string Empreinte,
    long Numero,
    IReadOnlyDictionary<string, int> Pings)
{
    public const string NomApplication = "pes6connect";
    public const int Version = 1;
    public const int TailleMax = 2048;
    public const int NombreMaxPings = 20;
    public const int PingMaxMs = 10_000;

    /// <summary>Port UDP utilisé par PES6 Connect (pas par le jeu).</summary>
    public const int Port = 47106;

    public static MessagePresence Annonce(string id, string nom, string empreinte, IReadOnlyDictionary<string, int> pings) =>
        new(TypeMessage.Annonce, id, nom, empreinte, 0, pings);

    public static MessagePresence Ping(string id, long numero) =>
        new(TypeMessage.Ping, id, "", "", numero, new Dictionary<string, int>());

    public static MessagePresence Pong(string id, long numero) =>
        new(TypeMessage.Pong, id, "", "", numero, new Dictionary<string, int>());

    public byte[] Encoder()
    {
        var pings = new JsonObject();
        foreach (var (id, ms) in Pings.Take(NombreMaxPings))
        {
            pings[id] = ms;
        }
        var json = new JsonObject
        {
            ["app"] = NomApplication,
            ["v"] = Version,
            ["t"] = Type switch { TypeMessage.Annonce => "annonce", TypeMessage.Ping => "ping", _ => "pong" },
            ["id"] = Id,
            ["n"] = Numero,
        };
        if (Type == TypeMessage.Annonce)
        {
            json["nom"] = Nom;
            json["emp"] = Empreinte;
            json["pings"] = pings;
        }
        return Encoding.UTF8.GetBytes(json.ToJsonString());
    }

    /// <summary>Lit un message reçu. Renvoie null s'il est invalide (il est alors ignoré).</summary>
    public static MessagePresence? Decoder(ReadOnlySpan<byte> donnees)
    {
        if (donnees.Length == 0 || donnees.Length > TailleMax)
        {
            return null;
        }
        try
        {
            using var doc = JsonDocument.Parse(donnees.ToArray());
            var e = doc.RootElement;
            if (e.ValueKind != JsonValueKind.Object
                || Texte(e, "app") != NomApplication
                || !e.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.Number || v.GetInt32() != Version)
            {
                return null;
            }

            var id = Texte(e, "id") ?? "";
            if (!EstIdentifiantValide(id))
            {
                return null;
            }
            var numero = e.TryGetProperty("n", out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetInt64(out var num) ? num : 0;

            switch (Texte(e, "t"))
            {
                case "ping":
                    return Ping(id, numero);
                case "pong":
                    return Pong(id, numero);
                case "annonce":
                    break;
                default:
                    return null;
            }

            var empreinte = (Texte(e, "emp") ?? "").ToLowerInvariant();
            if (empreinte.Length > 0 && !(empreinte.Length == 64 && empreinte.All(Uri.IsHexDigit)))
            {
                empreinte = "";
            }

            var pings = new Dictionary<string, int>();
            if (e.TryGetProperty("pings", out var p) && p.ValueKind == JsonValueKind.Object)
            {
                foreach (var entree in p.EnumerateObject().Take(NombreMaxPings))
                {
                    if (EstIdentifiantValide(entree.Name)
                        && entree.Value.ValueKind == JsonValueKind.Number
                        && entree.Value.TryGetInt32(out var ms) && ms is >= 0 and <= PingMaxMs)
                    {
                        pings[entree.Name.ToLowerInvariant()] = ms;
                    }
                }
            }

            return new MessagePresence(TypeMessage.Annonce, id.ToLowerInvariant(), NettoyerNom(Texte(e, "nom")), empreinte, numero, pings);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Identifiant ZeroTier d'un PC : 10 caractères hexadécimaux.</summary>
    public static bool EstIdentifiantValide(string id) => id.Length == 10 && id.All(Uri.IsHexDigit);

    /// <summary>Garde un nom affichable : pas de caractères de contrôle, 20 caractères au plus.</summary>
    public static string NettoyerNom(string? nom)
    {
        var propre = new string((nom ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (propre.Length > 20)
        {
            propre = propre[..20];
        }
        return propre.Length == 0 ? "Sans nom" : propre;
    }

    private static string? Texte(JsonElement e, string nom) =>
        e.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
