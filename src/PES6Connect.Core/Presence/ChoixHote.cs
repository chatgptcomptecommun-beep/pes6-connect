namespace PES6Connect.Core.Presence;

/// <summary>Résultat du choix de l'hôte. <see cref="EstMoi"/> = ce PC.</summary>
public sealed record SuggestionHote(string Id, string Nom, bool EstMoi, int PirePingMs, bool AEgalite);

/// <summary>
/// Choisit le meilleur hôte : celui dont le PIRE ping vers les autres joueurs est le plus petit.
/// (Dans PES 6, chaque joueur échange avec l'hôte : c'est le joueur le plus lent qui compte.)
/// </summary>
public static class ChoixHote
{
    public static SuggestionHote? Suggerer(
        string monId,
        string monNom,
        IReadOnlyDictionary<string, int> mesPings,
        IReadOnlyList<JoueurEnLigne> autres,
        Func<JoueurEnLigne, bool> peutJouer)
    {
        var participants = autres.Where(peutJouer).ToList();
        if (participants.Count == 0)
        {
            return null;
        }

        // Ping entre deux PC : moyenne des mesures connues dans les deux sens.
        int? Ping(string a, string b)
        {
            var mesures = new List<int>();
            void Ajouter(string de, string vers)
            {
                if (de == monId)
                {
                    if (mesPings.TryGetValue(vers, out var ms)) mesures.Add(ms);
                }
                else if (participants.FirstOrDefault(j => j.Id == de) is { } j && j.SesPings.TryGetValue(vers, out var ms2))
                {
                    mesures.Add(ms2);
                }
            }
            Ajouter(a, b);
            Ajouter(b, a);
            return mesures.Count == 0 ? null : (int)Math.Round(mesures.Average());
        }

        var ids = participants.Select(j => (j.Id, j.Nom)).Prepend((Id: monId, Nom: monNom)).ToList();
        var candidats = new List<(string Id, string Nom, int Pire)>();
        foreach (var (id, nom) in ids)
        {
            var pings = ids.Where(x => x.Id != id).Select(x => Ping(id, x.Id)).ToList();
            if (pings.All(p => p.HasValue))
            {
                candidats.Add((id, nom, pings.Max(p => p!.Value)));
            }
        }
        if (candidats.Count == 0)
        {
            return null;
        }

        var meilleur = candidats.OrderBy(c => c.Pire).ThenBy(c => c.Id, StringComparer.Ordinal).First();
        // À 5 ms près, deux hôtes se valent.
        var egalite = candidats.Count(c => c.Pire - meilleur.Pire <= 5) > 1;
        return new SuggestionHote(meilleur.Id, meilleur.Nom, meilleur.Id == monId, meilleur.Pire, egalite);
    }
}
