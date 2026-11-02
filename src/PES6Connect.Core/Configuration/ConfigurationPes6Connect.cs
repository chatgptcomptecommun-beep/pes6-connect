namespace PES6Connect.Core.Configuration;

/// <summary>
/// Contenu du fichier « pes6connect.json » placé à côté de PES6Connect.exe.
/// </summary>
public sealed class ConfigurationPes6Connect
{
    /// <summary>Prénom ou pseudo affiché aux autres joueurs.</summary>
    public string NomJoueur { get; set; } = "";

    /// <summary>Identifiant du réseau ZeroTier commun (16 caractères 0-9 / a-f).</summary>
    public string IdReseauZeroTier { get; set; } = "";

    /// <summary>Empreinte SHA-256 de référence de pes6.exe (utilisée à l'étape 4).</summary>
    public string EmpreintePes6 { get; set; } = "";

    /// <summary>Chemin de PES6.exe choisi à la main (vide = recherche automatique).</summary>
    public string CheminPes6 { get; set; } = "";
}
