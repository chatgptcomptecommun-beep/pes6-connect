using System.Text;
using PES6Connect.Core.Configuration;
using PES6Connect.Core.Pes6;
using PES6Connect.Core.Reseau;
using PES6Connect.Core.ZeroTier;

namespace PES6Connect.Core.Tests;

public sealed class CarteEtPes6Tests
{
    private static EtatCarteZeroTier Carte(string? categorie, int metrique, params AutreCarte[] autres) =>
        new(12, "ZeroTier One [633e31d8a269ea13]", categorie, metrique, true, autres);

    [Fact]
    public void Lit_le_json_du_script_powershell()
    {
        var carte = LecteurEtatCarte.Lire("""
            {"index":12,"alias":"ZeroTier One [633e31d8a269ea13]","categorie":"Public","metrique":35,"metriqueAuto":true,
             "autres":[{"alias":"Wi-Fi","metrique":45},{"alias":"Ethernet","metrique":25}]}
            """)!;

        Assert.Equal(12, carte.Index);
        Assert.Equal("Public", carte.Categorie);
        Assert.Equal(35, carte.Metrique);
        Assert.True(carte.MetriqueAutomatique);
        Assert.Equal("Ethernet", carte.CarteLaPlusPrioritaire!.Nom);
        Assert.False(carte.EstPrioritaire);
    }

    [Fact]
    public void Accepte_un_objet_seul_ou_une_carte_absente()
    {
        var carte = LecteurEtatCarte.Lire("""{"index":3,"alias":"ZT","categorie":null,"metrique":1,"metriqueAuto":false,"autres":{"alias":"Wi-Fi","metrique":45}}""")!;
        Assert.Single(carte.AutresCartes);
        Assert.Null(carte.Categorie);

        Assert.Null(LecteurEtatCarte.Lire("{}"));
    }

    [Fact]
    public void Prive_et_prioritaire_est_ok()
    {
        var d = DiagnosticCarte.Evaluer(Carte("Private", 1, new AutreCarte("Wi-Fi", 45)));
        Assert.Equal(NiveauVerification.Ok, d.Niveau);
    }

    [Fact]
    public void Public_propose_de_corriger()
    {
        var d = DiagnosticCarte.Evaluer(Carte("Public", 1, new AutreCarte("Wi-Fi", 45)));
        Assert.Equal(ActionProposee.CorrigerCarte, d.Action);
        Assert.Contains("Public", d.Message);
    }

    [Fact]
    public void Non_prioritaire_propose_de_corriger_et_nomme_l_autre_carte()
    {
        var d = DiagnosticCarte.Evaluer(Carte("Private", 35, new AutreCarte("Wi-Fi", 25)));
        Assert.Equal(ActionProposee.CorrigerCarte, d.Action);
        Assert.Contains("Wi-Fi", d.Message);
    }

    [Fact]
    public void Egalite_au_minimum_ne_propose_plus_de_correction()
    {
        var d = DiagnosticCarte.Evaluer(Carte("Private", 1, new AutreCarte("VPN", 1)));
        Assert.Equal(NiveauVerification.Attention, d.Niveau);
        Assert.Equal(ActionProposee.Aucune, d.Action);
        Assert.Contains("VPN", d.Message);
    }

    [Fact]
    public void Carte_introuvable_est_une_erreur() =>
        Assert.Equal(NiveauVerification.Erreur, DiagnosticCarte.Evaluer(null).Niveau);

    [Fact]
    public void Empreinte_sha256_connue()
    {
        // SHA-256 de « abc » (valeur de référence officielle).
        var e = EmpreinteFichier.CalculerSha256(new MemoryStream(Encoding.ASCII.GetBytes("abc")));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", e);
        Assert.True(ChargeurConfiguration.EstEmpreinteValide(e));
        Assert.Equal("ba7816bf…", EmpreinteFichier.Abreger(e));
    }

    private const string Ref = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void Pes6_cas_principaux()
    {
        Assert.Equal(ActionProposee.ChoisirPes6, DiagnosticPes6.Evaluer(null, null, Ref).Action);
        Assert.Equal(ActionProposee.ChoisirPes6, DiagnosticPes6.Evaluer(@"C:\PES6.exe", null, Ref).Action);
        Assert.Equal(ActionProposee.DefinirReference, DiagnosticPes6.Evaluer(@"C:\PES6.exe", Ref, "").Action);
        Assert.Equal(NiveauVerification.Ok, DiagnosticPes6.Evaluer(@"C:\PES6.exe", Ref, Ref.ToUpperInvariant()).Niveau);

        var different = DiagnosticPes6.Evaluer(@"C:\PES6.exe", new string('0', 64), Ref);
        Assert.Equal(NiveauVerification.Erreur, different.Niveau);
        Assert.Contains("différente", different.Message);
    }
}
