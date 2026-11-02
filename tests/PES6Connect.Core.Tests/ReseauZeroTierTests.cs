using PES6Connect.Core.ZeroTier;

namespace PES6Connect.Core.Tests;

public sealed class ReseauZeroTierTests
{
    // Extraits réalistes des réponses de l'API locale ZeroTier.
    private const string ReponseReseaux = """
        [
          {
            "id": "633E31D8A269EA13",
            "nwid": "633e31d8a269ea13",
            "name": "PES entre potes",
            "status": "OK",
            "type": "PRIVATE",
            "assignedAddresses": ["fd63:3e31:d8a2:69ea::1/88", "10.147.20.5/24"],
            "portDeviceName": "ZeroTier One [633e31d8a269ea13]"
          },
          { "id": "1111111111111111", "status": "ACCESS_DENIED", "assignedAddresses": [] }
        ]
        """;

    [Fact]
    public void Lit_la_liste_des_reseaux()
    {
        var reseaux = LecteurApiZeroTier.LireReseaux(ReponseReseaux);

        Assert.Equal(2, reseaux.Count);
        var r = reseaux[0];
        Assert.Equal("633e31d8a269ea13", r.Id);
        Assert.Equal("PES entre potes", r.Nom);
        Assert.Equal("OK", r.Statut);
        Assert.Equal("10.147.20.5", r.AdresseIPv4);
        Assert.Equal("ZeroTier One [633e31d8a269ea13]", r.NomCarte);
        Assert.Null(reseaux[1].AdresseIPv4);
        Assert.Equal("", reseaux[1].Nom);
    }

    [Fact]
    public void Lit_un_reseau_seul_et_l_identifiant_du_pc()
    {
        var r = LecteurApiZeroTier.LireReseau("""{ "id": "633e31d8a269ea13", "status": "REQUESTING_CONFIGURATION" }""");
        Assert.Equal("REQUESTING_CONFIGURATION", r.Statut);
        Assert.Empty(r.AdressesAttribuees);

        const string statut = """{ "address": "a1b2c3d4e5", "online": true, "version": "1.14.2" }""";
        Assert.Equal("a1b2c3d4e5", LecteurApiZeroTier.LireIdentifiantPc(statut));
        Assert.Equal("1.14.2", LecteurApiZeroTier.LireVersion(statut));
    }

    [Fact]
    public void Reponse_inattendue_donne_une_liste_vide() =>
        Assert.Empty(LecteurApiZeroTier.LireReseaux("""{ "erreur": true }"""));

    private static ReseauZeroTier Reseau(string statut, params string[] adresses) =>
        new("633e31d8a269ea13", "PES entre potes", statut, adresses, null);

    [Fact]
    public void Pas_encore_rejoint_propose_de_rejoindre()
    {
        var d = DiagnosticReseau.Evaluer(null, "a1b2c3d4e5");
        Assert.Equal(ActionProposee.Rejoindre, d.Action);
        Assert.False(DiagnosticReseau.EstEnAttente(null));
    }

    [Fact]
    public void Connecte_avec_adresse_est_ok()
    {
        var d = DiagnosticReseau.Evaluer(Reseau("OK", "10.147.20.5/24"), null);
        Assert.Equal(NiveauVerification.Ok, d.Niveau);
        Assert.Contains("10.147.20.5", d.Message);
        Assert.False(DiagnosticReseau.EstEnAttente(Reseau("OK", "10.147.20.5/24")));
    }

    [Theory]
    [InlineData("REQUESTING_CONFIGURATION")]
    [InlineData("ACCESS_DENIED")]
    public void En_attente_d_approbation_affiche_l_identifiant(string statut)
    {
        var d = DiagnosticReseau.Evaluer(Reseau(statut), "a1b2c3d4e5");
        Assert.Equal(NiveauVerification.Attention, d.Niveau);
        Assert.Contains("a1b2c3d4e5", d.Message);
        Assert.True(DiagnosticReseau.EstEnAttente(Reseau(statut)));
    }

    [Fact]
    public void Ok_sans_adresse_est_en_attente()
    {
        Assert.Equal(NiveauVerification.Attention, DiagnosticReseau.Evaluer(Reseau("OK"), null).Niveau);
        Assert.True(DiagnosticReseau.EstEnAttente(Reseau("OK")));
    }

    [Theory]
    [InlineData("NOT_FOUND")]
    [InlineData("PORT_ERROR")]
    [InlineData("CLIENT_TOO_OLD")]
    [InlineData("AUTHENTICATION_REQUIRED")]
    public void Erreurs_sont_signalees(string statut) =>
        Assert.Equal(NiveauVerification.Erreur, DiagnosticReseau.Evaluer(Reseau(statut), null).Niveau);
}
