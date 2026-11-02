using PES6Connect.Core.Reseau;
using PES6Connect.Core.ZeroTier;

namespace PES6Connect.Core.Tests;

public sealed class PareFeuTests
{
    private const string Pes6 = @"C:\Program Files (x86)\KONAMI\Pro Evolution Soccer 6\PES6.exe";

    private static EtatPareFeu Etat(bool existe = true, bool active = true, string? programme = Pes6,
        string? profils = "Domain, Private", params string[] blocages) =>
        new(existe, active, programme, profils, blocages);

    [Fact]
    public void Lit_le_json_du_script()
    {
        var e = PareFeu.Lire("""
            {"regleExiste":true,"regleActive":true,"programme":"C:\\Program Files (x86)\\KONAMI\\Pro Evolution Soccer 6\\PES6.exe",
             "profils":"Domain, Private","blocages":["pro evolution soccer 6","PES6"]}
            """);
        Assert.True(e.RegleExiste);
        Assert.Equal(Pes6, e.Programme);
        Assert.Equal(2, e.Blocages.Count);

        // Règle absente, pas de blocage, valeurs nulles.
        var vide = PareFeu.Lire("""{"regleExiste":false,"regleActive":false,"programme":null,"profils":null,"blocages":[]}""");
        Assert.False(vide.RegleExiste);
        Assert.Empty(vide.Blocages);

        // PowerShell peut écrire une chaîne seule au lieu d'une liste.
        Assert.Single(PareFeu.Lire("""{"blocages":"PES6"}""").Blocages);
    }

    [Fact]
    public void Regle_correcte_est_ok_meme_avec_une_casse_differente() =>
        Assert.Equal(NiveauVerification.Ok, PareFeu.Evaluer(Etat(programme: Pes6.ToLowerInvariant()), Pes6).Niveau);

    [Fact]
    public void Regle_absente_propose_de_corriger() =>
        Assert.Equal(ActionProposee.CorrigerPareFeu, PareFeu.Evaluer(Etat(existe: false, active: false, programme: null, profils: null), Pes6).Action);

    [Theory]
    [InlineData(false, Pes6, "Private")]
    [InlineData(true, @"D:\Autre\PES6.exe", "Private")]
    [InlineData(true, Pes6, "Public")]
    public void Regle_incorrecte_propose_de_corriger(bool active, string programme, string profils) =>
        Assert.Equal(ActionProposee.CorrigerPareFeu, PareFeu.Evaluer(Etat(active: active, programme: programme, profils: profils), Pes6).Action);

    [Fact]
    public void Blocage_l_emporte_et_est_nomme()
    {
        var d = PareFeu.Evaluer(Etat(blocages: ["pro evolution soccer 6"]), Pes6);
        Assert.Equal(ActionProposee.CorrigerPareFeu, d.Action);
        Assert.Contains("pro evolution soccer 6", d.Message);
    }

    [Fact]
    public void Regle_de_l_outil_manquante_propose_de_corriger()
    {
        var d = PareFeu.Evaluer(Etat() with { RegleOutilOk = false }, Pes6);
        Assert.Equal(ActionProposee.CorrigerPareFeu, d.Action);
        Assert.Contains("PES6 Connect", d.Message);
        Assert.False(PareFeu.Lire("{}").RegleOutilOk);
        Assert.True(PareFeu.Lire("{\"regleOutil\":true}").RegleOutilOk);
    }

    [Fact]
    public void Apostrophes_sont_doublees_pour_powershell() =>
        Assert.Equal("'C:\\Jeux d''Awa\\PES6.exe'", PareFeu.ChaineCitee(@"C:\Jeux d'Awa\PES6.exe"));
}
