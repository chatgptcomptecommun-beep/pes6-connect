using System.Net;
using System.Text;
using PES6Connect.Core.Presence;
using PES6Connect.Core.Reseau;
using PES6Connect.Core.ZeroTier;

namespace PES6Connect.Core.Tests;

public sealed class PresenceTests
{
    private const string Moi = "ca22fa7f12";
    private const string Karim = "aaaaaaaaaa";
    private const string Lea = "bbbbbbbbbb";
    private static readonly string Ref = new('a', 64);
    private static readonly IPAddress IpKarim = IPAddress.Parse("10.134.125.30");
    private static readonly IPAddress IpLea = IPAddress.Parse("10.134.125.31");

    private static Dictionary<string, int> P(params (string Id, int Ms)[] pings) => pings.ToDictionary(p => p.Id, p => p.Ms);

    [Fact]
    public void Annonce_aller_retour()
    {
        var m = MessagePresence.Annonce(Moi, "kirillos26", Ref, P((Karim, 42)));
        var lu = MessagePresence.Decoder(m.Encoder())!;
        Assert.Equal(TypeMessage.Annonce, lu.Type);
        Assert.Equal(Moi, lu.Id);
        Assert.Equal("kirillos26", lu.Nom);
        Assert.Equal(Ref, lu.Empreinte);
        Assert.Equal(42, lu.Pings[Karim]);

        var ping = MessagePresence.Decoder(MessagePresence.Ping(Moi, 123).Encoder())!;
        Assert.Equal((TypeMessage.Ping, 123L), (ping.Type, ping.Numero));
    }

    [Theory]
    [InlineData("")]
    [InlineData("pas du json")]
    [InlineData("""{"app":"autre","v":1,"t":"ping","id":"ca22fa7f12"}""")]
    [InlineData("""{"app":"pes6connect","v":2,"t":"ping","id":"ca22fa7f12"}""")]
    [InlineData("""{"app":"pes6connect","v":1,"t":"ping","id":"pirate"}""")]
    [InlineData("""{"app":"pes6connect","v":1,"t":"inconnu","id":"ca22fa7f12"}""")]
    [InlineData("""[1,2,3]""")]
    public void Messages_invalides_sont_ignores(string texte) =>
        Assert.Null(MessagePresence.Decoder(Encoding.UTF8.GetBytes(texte)));

    [Fact]
    public void Message_trop_gros_est_ignore() =>
        Assert.Null(MessagePresence.Decoder(new byte[MessagePresence.TailleMax + 1]));

    [Fact]
    public void Champs_douteux_sont_nettoyes()
    {
        var json = """{"app":"pes6connect","v":1,"t":"annonce","id":"AAAAAAAAAA","nom":"Un nom beaucoup trop long\u0007 pour l'écran","emp":"pas-une-empreinte","pings":{"bbbbbbbbbb":30,"faux":5,"cccccccccc":-3,"dddddddddd":999999}}""";
        var m = MessagePresence.Decoder(Encoding.UTF8.GetBytes(json))!;
        Assert.Equal(Karim, m.Id);
        Assert.Equal(20, m.Nom.Length);
        Assert.DoesNotContain('\u0007', m.Nom);
        Assert.Equal("", m.Empreinte);
        Assert.Equal(Lea, Assert.Single(m.Pings).Key);
    }

    [Fact]
    public void Table_mesure_les_pings_et_oublie_les_absents()
    {
        var t = new TablePresence(Moi);
        t.RecevoirAnnonce(MessagePresence.Annonce(Karim, "Karim", Ref, P()), IpKarim, 0);
        t.RecevoirAnnonce(MessagePresence.Annonce(Moi, "moi", Ref, P()), IPAddress.Loopback, 0); // ma propre annonce : ignorée

        var (adresse, numero) = Assert.Single(t.PreparerPings(1000));
        Assert.Equal(IpKarim, adresse);
        t.RecevoirPong(MessagePresence.Pong(Karim, numero), 1040);
        t.RecevoirPong(MessagePresence.Pong(Karim, numero), 1500); // doublon : ignoré
        t.RecevoirPong(MessagePresence.Pong(Lea, 999), 1500);     // inconnu : ignoré

        var karim = Assert.Single(t.Joueurs(1100));
        Assert.Equal(40, karim.PingMs);
        Assert.Equal(40, t.MesPings(1100)[Karim]);

        Assert.Empty(t.Joueurs(TablePresence.ExpirationMs + 1));
    }

    [Fact]
    public void Pong_d_un_autre_pc_avec_le_bon_numero_est_refuse()
    {
        var t = new TablePresence(Moi);
        t.RecevoirAnnonce(MessagePresence.Annonce(Karim, "Karim", Ref, P()), IpKarim, 0);
        t.RecevoirAnnonce(MessagePresence.Annonce(Lea, "Léa", Ref, P()), IpLea, 0);
        var numeroKarim = t.PreparerPings(0).Single(p => p.Adresse.Equals(IpKarim)).Numero;

        t.RecevoirPong(MessagePresence.Pong(Lea, numeroKarim), 10);

        Assert.All(t.Joueurs(20), j => Assert.Null(j.PingMs));
    }

    private static JoueurEnLigne J(string id, string nom, Dictionary<string, int> sesPings, string? empreinte = null) =>
        new(id, nom, IPAddress.Loopback, empreinte ?? Ref, null, sesPings);

    [Fact]
    public void A_trois_l_hote_est_celui_dont_le_pire_ping_est_le_plus_petit()
    {
        // Moi–Karim 40, Moi–Léa 150, Karim–Léa 60 → Karim : max(40,60)=60 ; moi : 150 ; Léa : 150.
        var s = ChoixHote.Suggerer(Moi, "moi", P((Karim, 40), (Lea, 150)),
            [J(Karim, "Karim", P((Moi, 40), (Lea, 60))), J(Lea, "Léa", P((Moi, 150), (Karim, 60)))], _ => true)!;

        Assert.Equal(Karim, s.Id);
        Assert.Equal(60, s.PirePingMs);
        Assert.False(s.EstMoi);
        Assert.False(s.AEgalite);
    }

    [Fact]
    public void A_deux_c_est_egalite()
    {
        var s = ChoixHote.Suggerer(Moi, "moi", P((Karim, 40)), [J(Karim, "Karim", P((Moi, 44)))], _ => true)!;
        Assert.True(s.AEgalite);
        Assert.Equal(42, s.PirePingMs);
    }

    [Fact]
    public void Pas_de_suggestion_sans_mesures_ou_sans_joueur_valide()
    {
        Assert.Null(ChoixHote.Suggerer(Moi, "moi", P(), [J(Karim, "Karim", P())], _ => true));
        Assert.Null(ChoixHote.Suggerer(Moi, "moi", P((Karim, 40)), [J(Karim, "Karim", P((Moi, 40)))], _ => false));
    }

    [Fact]
    public void Affichage_signale_relais_et_mauvaise_version()
    {
        var joueurs = new[]
        {
            J(Karim, "Karim", P()) with { PingMs = 42 },
            J(Lea, "Léa", P(), empreinte: new string('b', 64)),
        };
        var (niveau, texte) = AffichageJoueurs.Rediger(joueurs, new HashSet<string> { Karim }, Ref,
            new SuggestionHote(Moi, "moi", true, 42, false));

        Assert.Equal(NiveauVerification.Attention, niveau);
        Assert.Contains("Karim : 42 ms · connexion relayée", texte);
        Assert.Contains("Léa : mesure en cours… · version de PES 6 différente", texte);
        Assert.Contains("Hôte conseillé : toi", texte);

        var (vide, attente) = AffichageJoueurs.Rediger([], new HashSet<string>(), Ref, null);
        Assert.Null(vide);
        Assert.Contains("Aucun autre joueur", attente);
    }

    [Fact]
    public void Calculs_sur_le_reseau_ipv4()
    {
        var r = ReseauIPv4.Lire("10.134.125.26/24")!;
        Assert.Equal(IPAddress.Parse("10.134.125.255"), r.Diffusion);
        Assert.True(r.Contient(IPAddress.Parse("10.134.125.1")));
        Assert.False(r.Contient(IPAddress.Parse("192.168.1.10")));
        Assert.Equal(IPAddress.Parse("10.147.255.255"), ReseauIPv4.Lire("10.147.20.5/16")!.Diffusion);
        Assert.Null(ReseauIPv4.Lire("10.134.125.26"));
        Assert.Null(ReseauIPv4.Lire("fd00::1/88"));
    }

    [Fact]
    public void Detecte_les_pairs_relayes()
    {
        var relayes = LecteurApiZeroTier.LirePairsRelayes("""
            [
              {"address":"aaaaaaaaaa","role":"LEAF","paths":[{"active":true,"expired":false,"address":"1.2.3.4/9993"}]},
              {"address":"bbbbbbbbbb","role":"LEAF","paths":[]},
              {"address":"cccccccccc","role":"LEAF","paths":[{"active":false,"expired":true}]},
              {"address":"62f865ae71","role":"PLANET","paths":[]}
            ]
            """);
        Assert.Equal(new HashSet<string> { Lea, "cccccccccc" }, relayes);
    }
}
