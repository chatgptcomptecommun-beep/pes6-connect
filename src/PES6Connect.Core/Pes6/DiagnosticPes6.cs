using System.Security.Cryptography;
using PES6Connect.Core.ZeroTier;

namespace PES6Connect.Core.Pes6;

/// <summary>Empreinte (« ADN ») d'un fichier : change dès qu'un seul octet est différent.</summary>
public static class EmpreinteFichier
{
    public static string CalculerSha256(Stream contenu) =>
        Convert.ToHexString(SHA256.HashData(contenu)).ToLowerInvariant();

    public static string CalculerSha256(string chemin)
    {
        using var fichier = File.OpenRead(chemin);
        return CalculerSha256(fichier);
    }

    /// <summary>Version courte pour l'affichage : « 1a2b3c4d… ».</summary>
    public static string Abreger(string empreinte) =>
        empreinte.Length <= 8 ? empreinte : empreinte[..8] + "…";
}

public static class DiagnosticPes6
{
    /// <param name="chemin">Chemin de PES6.exe trouvé, ou null.</param>
    /// <param name="empreinte">Empreinte calculée, ou null si le fichier est illisible.</param>
    /// <param name="reference">Empreinte de référence du fichier de configuration (peut être vide).</param>
    public static ResultatVerification Evaluer(string? chemin, string? empreinte, string reference)
    {
        if (chemin is null)
        {
            return new(NiveauVerification.Erreur,
                "PES 6 est introuvable. Clique sur « Choisir PES6.exe » et indique où il est installé.", ActionProposee.ChoisirPes6);
        }
        if (empreinte is null)
        {
            return new(NiveauVerification.Erreur,
                $"Impossible de lire {chemin}. Vérifie que le jeu n'est pas lancé, ou choisis un autre fichier.", ActionProposee.ChoisirPes6);
        }
        if (string.IsNullOrEmpty(reference))
        {
            return new(NiveauVerification.Attention,
                $"PES 6 trouvé (empreinte {EmpreinteFichier.Abreger(empreinte)}). Aucune référence n'est encore définie : "
                + "si ce PC sert de référence pour le groupe, clique sur « En faire la référence ».", ActionProposee.DefinirReference);
        }
        if (string.Equals(empreinte, reference, StringComparison.OrdinalIgnoreCase))
        {
            return new(NiveauVerification.Ok,
                "PES 6 trouvé, version identique à celle du groupe.", ActionProposee.Aucune);
        }
        return new(NiveauVerification.Erreur,
            $"Ta version de PES 6 est différente de celle du groupe (empreinte {EmpreinteFichier.Abreger(empreinte)} au lieu de "
            + $"{EmpreinteFichier.Abreger(reference)}). Il faut PES 6 version 1.0 française, sans patch. "
            + "Si ce n'est pas le bon fichier, clique sur « Choisir PES6.exe ».", ActionProposee.ChoisirPes6);
    }
}
