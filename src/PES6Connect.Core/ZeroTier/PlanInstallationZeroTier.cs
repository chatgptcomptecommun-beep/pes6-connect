namespace PES6Connect.Core.ZeroTier;

/// <summary>
/// Comment installer ZeroTier : d'abord avec winget (l'installateur intégré à
/// Windows 10/11), sinon en ouvrant la page de téléchargement officielle.
/// </summary>
public static class PlanInstallationZeroTier
{
    public const string IdPaquetWinget = "ZeroTier.ZeroTierOne";
    public const string PageTelechargement = "https://www.zerotier.com/download/";

    /// <summary>Durée maximale laissée à winget avant d'abandonner.</summary>
    public static readonly TimeSpan DelaiMaxInstallation = TimeSpan.FromMinutes(10);

    public static IReadOnlyList<string> ArgumentsWinget() =>
    [
        "install",
        "--id", IdPaquetWinget,
        "--exact",
        "--source", "winget",
        "--silent",
        "--accept-package-agreements",
        "--accept-source-agreements",
        "--disable-interactivity",
    ];

    /// <summary>Traduit le code de sortie de winget en phrase compréhensible.</summary>
    public static string ExpliquerCodeSortie(int code) => unchecked((uint)code) switch
    {
        0 => "Installation terminée.",
        0x8A150061 or 0x8A15002B => "ZeroTier était déjà installé.",
        0x8A150014 => "winget n'a pas trouvé ZeroTier dans son catalogue.",
        0x8A15000F or 0x8A150008 => "winget n'a pas pu télécharger ZeroTier (vérifie ta connexion Internet).",
        var autre => $"L'installation automatique a échoué (code 0x{autre:X8}).",
    };
}
