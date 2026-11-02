using System.Net;

namespace PES6Connect.Core.Presence;

/// <summary>Un autre joueur actuellement en ligne.</summary>
public sealed record JoueurEnLigne(
    string Id,
    string Nom,
    IPAddress Adresse,
    string Empreinte,
    int? PingMs,
    IReadOnlyDictionary<string, int> SesPings);

/// <summary>
/// Tient à jour la liste des joueurs en ligne et les pings mesurés.
/// Utilisable depuis plusieurs fils d'exécution (verrou interne).
/// Le temps est fourni en millisecondes par l'appelant, ce qui rend la classe testable.
/// </summary>
public sealed class TablePresence(string monId)
{
    /// <summary>Un joueur disparaît de la liste s'il ne s'est pas annoncé depuis ce délai.</summary>
    public const long ExpirationMs = 10_000;
    /// <summary>Un ping sans réponse après ce délai est abandonné.</summary>
    public const long DelaiPingMs = 5_000;
    /// <summary>Nombre de mesures gardées pour la moyenne.</summary>
    public const int MesuresGardees = 5;
    public const int JoueursMax = 20;

    private sealed class Entree
    {
        public required string Nom;
        public required IPAddress Adresse;
        public required string Empreinte;
        public required IReadOnlyDictionary<string, int> SesPings;
        public long VuA;
        public readonly Queue<int> Mesures = new();
    }

    private readonly object _verrou = new();
    private readonly Dictionary<string, Entree> _joueurs = new();
    private readonly Dictionary<long, (string Id, long EnvoyeA)> _pingsEnAttente = new();
    private long _prochainNumero = Random.Shared.NextInt64(1, long.MaxValue / 2);

    public string MonId { get; } = monId.ToLowerInvariant();

    public void RecevoirAnnonce(MessagePresence annonce, IPAddress source, long maintenantMs)
    {
        if (annonce.Type != TypeMessage.Annonce || annonce.Id == MonId)
        {
            return;
        }
        lock (_verrou)
        {
            if (!_joueurs.TryGetValue(annonce.Id, out var e))
            {
                if (_joueurs.Count >= JoueursMax)
                {
                    return;
                }
                e = new Entree { Nom = annonce.Nom, Adresse = source, Empreinte = annonce.Empreinte, SesPings = annonce.Pings };
                _joueurs[annonce.Id] = e;
            }
            e.Nom = annonce.Nom;
            e.Adresse = source;
            e.Empreinte = annonce.Empreinte;
            e.SesPings = annonce.Pings;
            e.VuA = maintenantMs;
        }
    }

    /// <summary>Prépare les pings à envoyer à chaque joueur connu : (adresse, numéro).</summary>
    public IReadOnlyList<(IPAddress Adresse, long Numero)> PreparerPings(long maintenantMs)
    {
        lock (_verrou)
        {
            Purger(maintenantMs);
            var liste = new List<(IPAddress, long)>();
            foreach (var (id, e) in _joueurs)
            {
                var numero = _prochainNumero++;
                _pingsEnAttente[numero] = (id, maintenantMs);
                liste.Add((e.Adresse, numero));
            }
            return liste;
        }
    }

    public void RecevoirPong(MessagePresence pong, long maintenantMs)
    {
        lock (_verrou)
        {
            if (pong.Type != TypeMessage.Pong
                || !_pingsEnAttente.Remove(pong.Numero, out var attente)
                || attente.Id != pong.Id
                || !_joueurs.TryGetValue(pong.Id, out var e))
            {
                return;
            }
            var ms = (int)Math.Clamp(maintenantMs - attente.EnvoyeA, 0, MessagePresence.PingMaxMs);
            e.Mesures.Enqueue(ms);
            while (e.Mesures.Count > MesuresGardees)
            {
                e.Mesures.Dequeue();
            }
        }
    }

    /// <summary>Pings moyens mesurés par ce PC (envoyés dans l'annonce).</summary>
    public IReadOnlyDictionary<string, int> MesPings(long maintenantMs)
    {
        lock (_verrou)
        {
            Purger(maintenantMs);
            return _joueurs.Where(j => j.Value.Mesures.Count > 0)
                .ToDictionary(j => j.Key, j => (int)Math.Round(j.Value.Mesures.Average()));
        }
    }

    public IReadOnlyList<JoueurEnLigne> Joueurs(long maintenantMs)
    {
        lock (_verrou)
        {
            Purger(maintenantMs);
            return _joueurs
                .Select(j => new JoueurEnLigne(j.Key, j.Value.Nom, j.Value.Adresse, j.Value.Empreinte,
                    j.Value.Mesures.Count > 0 ? (int)Math.Round(j.Value.Mesures.Average()) : null, j.Value.SesPings))
                .OrderBy(j => j.Nom, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }

    private void Purger(long maintenantMs)
    {
        foreach (var id in _joueurs.Where(j => maintenantMs - j.Value.VuA > ExpirationMs).Select(j => j.Key).ToList())
        {
            _joueurs.Remove(id);
        }
        foreach (var numero in _pingsEnAttente.Where(p => maintenantMs - p.Value.EnvoyeA > DelaiPingMs).Select(p => p.Key).ToList())
        {
            _pingsEnAttente.Remove(numero);
        }
    }
}
