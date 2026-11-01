using System.Text.Json;

namespace PES6Connect.Core.Configuration;

public enum EtatConfiguration
{
    /// <summary>Le fichier existe et tout est rempli correctement.</summary>
    Valide,
    /// <summary>Le fichier est lisible mais certaines valeurs manquent ou sont fausses.</summary>
    Incomplete,
    /// <summary>Le fichier n'existait pas : un modèle vide vient d'être créé.</summary>
    FichierCree,
    /// <summary>Le fichier est illisible ou n'a pas pu être créé.</summary>
    Illisible,
}

public sealed record ResultatConfiguration(
    EtatConfiguration Etat,
    ConfigurationPes6Connect? Configuration,
    IReadOnlyList<string> Problemes);

/// <summary>
/// Lit et vérifie le fichier de configuration. Ne lève jamais d'exception :
/// tout problème est décrit en français dans <see cref="ResultatConfiguration.Problemes"/>.
/// </summary>
public static class ChargeurConfiguration
{
    public const string NomFichier = "pes6connect.json";
    public const int LongueurMaxNom = 20;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public static ResultatConfiguration Charger(string chemin)
    {
        if (!File.Exists(chemin))
        {
            return CreerModele(chemin);
        }

        ConfigurationPes6Connect? config;
        try
        {
            config = JsonSerializer.Deserialize<ConfigurationPes6Connect>(File.ReadAllText(chemin), Options);
        }
        catch (JsonException ex)
        {
            var ligne = ex.LineNumber is long n ? $" (vers la ligne {n + 1})" : "";
            return new(EtatConfiguration.Illisible, null,
                [$"Le fichier {NomFichier} contient une erreur de format{ligne}. Vérifie les guillemets et les virgules."]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(EtatConfiguration.Illisible, null,
                [$"Impossible de lire {NomFichier} : {ex.Message}"]);
        }

        if (config is null)
        {
            return new(EtatConfiguration.Illisible, null, [$"Le fichier {NomFichier} est vide."]);
        }

        Normaliser(config);
        var problemes = Valider(config);
        return new(problemes.Count == 0 ? EtatConfiguration.Valide : EtatConfiguration.Incomplete, config, problemes);
    }

    public static IReadOnlyList<string> Valider(ConfigurationPes6Connect config)
    {
        var problemes = new List<string>();

        if (string.IsNullOrWhiteSpace(config.NomJoueur))
        {
            problemes.Add("Indique ton prénom ou pseudo dans « nomJoueur ».");
        }
        else if (config.NomJoueur.Length > LongueurMaxNom)
        {
            problemes.Add($"Le nom du joueur doit faire au plus {LongueurMaxNom} caractères.");
        }

        if (string.IsNullOrEmpty(config.IdReseauZeroTier))
        {
            problemes.Add("L'identifiant du réseau ZeroTier (« idReseauZeroTier ») n'est pas renseigné.");
        }
        else if (!EstIdReseauValide(config.IdReseauZeroTier))
        {
            problemes.Add("L'identifiant du réseau ZeroTier doit faire 16 caractères (chiffres 0-9 et lettres a-f).");
        }

        // L'empreinte de PES 6 est facultative tant que l'étape 4 n'existe pas.
        return problemes;
    }

    /// <summary>Un ID de réseau ZeroTier = 16 caractères hexadécimaux, ex. « 8056c2e21c000001 ».</summary>
    public static bool EstIdReseauValide(string id) =>
        id.Length == 16 && id.All(Uri.IsHexDigit);

    private static void Normaliser(ConfigurationPes6Connect config)
    {
        config.NomJoueur = (config.NomJoueur ?? "").Trim();
        config.IdReseauZeroTier = (config.IdReseauZeroTier ?? "").Trim().ToLowerInvariant();
        config.EmpreintePes6 = (config.EmpreintePes6 ?? "").Trim().ToLowerInvariant();
    }

    private static ResultatConfiguration CreerModele(string chemin)
    {
        var modele = new ConfigurationPes6Connect();
        try
        {
            File.WriteAllText(chemin, JsonSerializer.Serialize(modele, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(EtatConfiguration.Illisible, null,
                [$"Le fichier {NomFichier} est absent et n'a pas pu être créé ({ex.Message}). "
                 + "Si tu as lancé l'outil depuis le fichier .zip, extrais d'abord le dossier."]);
        }

        return new(EtatConfiguration.FichierCree, modele,
            [$"Le fichier {NomFichier} vient d'être créé à côté de l'outil : ouvre-le avec le Bloc-notes et remplis-le."]);
    }
}
