using System.Net;
using System.Text;
using PES6Connect.Core.ZeroTier;

namespace PES6Connect.App;

/// <summary>Erreur avec un message déjà rédigé pour le joueur.</summary>
internal sealed class ErreurZeroTier(string message) : Exception(message);

/// <summary>
/// Dialogue avec le service ZeroTier installé sur ce PC, via son « guichet » local
/// (http://127.0.0.1:9993). Le mot de passe de ce guichet est le fichier
/// authtoken.secret, lisible seulement en administrateur.
/// </summary>
internal sealed class ClientApiZeroTier : IDisposable
{
    private const int PortParDefaut = 9993;

    private readonly HttpClient _http = new(new HttpClientHandler { UseProxy = false })
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    private static string Dossier => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ZeroTier", "One");

    public void Dispose() => _http.Dispose();

    /// <summary>
    /// Identifiant ZeroTier de ce PC (10 caractères, à donner à l'organisatrice) et version de ZeroTier.
    /// </summary>
    public async Task<(string? IdentifiantPc, string? Version)> LireStatutAsync()
    {
        var json = await EnvoyerAsync(HttpMethod.Get, "status");
        return (LecteurApiZeroTier.LireIdentifiantPc(json), LecteurApiZeroTier.LireVersion(json));
    }

    /// <summary>Le réseau demandé s'il a déjà été rejoint, sinon null.</summary>
    public async Task<ReseauZeroTier?> LireReseauAsync(string idReseau) =>
        LecteurApiZeroTier.LireReseaux(await EnvoyerAsync(HttpMethod.Get, "network"))
            .FirstOrDefault(r => r.Id == idReseau);

    /// <summary>Identifiants des PC joints par un relais ZeroTier (connexion lente).</summary>
    public async Task<IReadOnlySet<string>> LirePairsRelayesAsync() =>
        LecteurApiZeroTier.LirePairsRelayes(await EnvoyerAsync(HttpMethod.Get, "peer"));

    /// <summary>Demande à rejoindre le réseau (sans effet si c'est déjà fait).</summary>
    public async Task<ReseauZeroTier> RejoindreAsync(string idReseau) =>
        LecteurApiZeroTier.LireReseau(await EnvoyerAsync(HttpMethod.Post, $"network/{idReseau}", "{}"));

    private async Task<string> EnvoyerAsync(HttpMethod methode, string chemin, string? corps = null)
    {
        var (port, jeton) = LireAcces();
        using var requete = new HttpRequestMessage(methode, $"http://127.0.0.1:{port}/{chemin}");
        requete.Headers.Add("X-ZT1-Auth", jeton);
        if (corps is not null)
        {
            requete.Content = new StringContent(corps, Encoding.UTF8, "application/json");
        }

        try
        {
            using var reponse = await _http.SendAsync(requete);
            var texte = await reponse.Content.ReadAsStringAsync();
            if (reponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new ErreurZeroTier("ZeroTier a refusé l'accès à PES6 Connect. Redémarre le PC puis réessaie.");
            }
            if (!reponse.IsSuccessStatusCode)
            {
                throw new ErreurZeroTier($"ZeroTier a répondu par une erreur ({(int)reponse.StatusCode}). Réessaie dans quelques secondes.");
            }
            return texte;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ErreurZeroTier("Le service ZeroTier ne répond pas. Clique sur « Tout revérifier » dans quelques secondes.");
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ErreurZeroTier("Réponse de ZeroTier incompréhensible. Mets ZeroTier à jour puis réessaie.");
        }
    }

    private static (int Port, string Jeton) LireAcces()
    {
        // Si le port 9993 est déjà pris, ZeroTier note le port réellement utilisé dans ce fichier.
        var port = PortParDefaut;
        try
        {
            var fichierPort = Path.Combine(Dossier, "zerotier-one.port");
            if (File.Exists(fichierPort) && int.TryParse(File.ReadAllText(fichierPort).Trim(), out var p) && p is > 0 and < 65536)
            {
                port = p;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // On garde le port par défaut.
        }

        try
        {
            var jeton = File.ReadAllText(Path.Combine(Dossier, "authtoken.secret")).Trim();
            if (jeton.Length == 0)
            {
                throw new ErreurZeroTier("Le fichier d'accès de ZeroTier est vide. Réinstalle ZeroTier.");
            }
            return (port, jeton);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new ErreurZeroTier("Le fichier d'accès de ZeroTier est introuvable. Réinstalle ZeroTier.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new ErreurZeroTier("Accès refusé : relance PES6 Connect et réponds « Oui » à la demande administrateur.");
        }
    }
}
