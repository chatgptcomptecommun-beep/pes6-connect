using PES6Connect.Core.Configuration;

namespace PES6Connect.Core.Tests;

public sealed class ChargeurConfigurationTests : IDisposable
{
    private readonly string _dossier = Directory.CreateTempSubdirectory("pes6connect-tests").FullName;
    private string Chemin => Path.Combine(_dossier, ChargeurConfiguration.NomFichier);

    public void Dispose() => Directory.Delete(_dossier, recursive: true);

    [Fact]
    public void Fichier_absent_cree_un_modele_vide()
    {
        var r = ChargeurConfiguration.Charger(Chemin);

        Assert.Equal(EtatConfiguration.FichierCree, r.Etat);
        Assert.True(File.Exists(Chemin));
        Assert.Contains("nomJoueur", File.ReadAllText(Chemin));
        // Le modèle créé doit être relu sans erreur de format.
        Assert.Equal(EtatConfiguration.Incomplete, ChargeurConfiguration.Charger(Chemin).Etat);
    }

    [Fact]
    public void Fichier_complet_est_valide_et_normalise()
    {
        File.WriteAllText(Chemin, """
            {
              // commentaire autorisé
              "nomJoueur": "  Awa ",
              "idReseauZeroTier": "8056C2E21C000001",
              "empreintePes6": "",
            }
            """);

        var r = ChargeurConfiguration.Charger(Chemin);

        Assert.Equal(EtatConfiguration.Valide, r.Etat);
        Assert.Empty(r.Problemes);
        Assert.Equal("Awa", r.Configuration!.NomJoueur);
        Assert.Equal("8056c2e21c000001", r.Configuration.IdReseauZeroTier);
    }

    [Fact]
    public void Json_casse_donne_un_message_avec_la_ligne()
    {
        File.WriteAllText(Chemin, "{\n \"nomJoueur\": \"Awa\"\n \"idReseauZeroTier\": \"x\" }");

        var r = ChargeurConfiguration.Charger(Chemin);

        Assert.Equal(EtatConfiguration.Illisible, r.Etat);
        Assert.Contains("ligne", Assert.Single(r.Problemes));
    }

    [Fact]
    public void Valeurs_manquantes_ou_fausses_sont_signalees()
    {
        File.WriteAllText(Chemin, """{ "nomJoueur": "", "idReseauZeroTier": "1234" }""");

        var r = ChargeurConfiguration.Charger(Chemin);

        Assert.Equal(EtatConfiguration.Incomplete, r.Etat);
        Assert.Equal(2, r.Problemes.Count);
    }

    [Theory]
    [InlineData("8056c2e21c000001", true)]
    [InlineData("8056C2E21C000001", true)]
    [InlineData("8056c2e21c00000", false)]
    [InlineData("8056c2e21c0000011", false)]
    [InlineData("8056c2e21c00000g", false)]
    [InlineData("", false)]
    public void Format_de_l_id_reseau(string id, bool attendu) =>
        Assert.Equal(attendu, ChargeurConfiguration.EstIdReseauValide(id));
}

public sealed class ModificationConfigurationTests : IDisposable
{
    private readonly string _dossier = Directory.CreateTempSubdirectory("pes6connect-tests").FullName;
    private string Chemin => Path.Combine(_dossier, ChargeurConfiguration.NomFichier);

    public void Dispose() => Directory.Delete(_dossier, recursive: true);

    [Fact]
    public void Modifier_conserve_les_autres_valeurs()
    {
        File.WriteAllText(Chemin, """{ "nomJoueur": "Awa", "idReseauZeroTier": "633e31d8a269ea13" }""");

        var erreur = ChargeurConfiguration.Modifier(Chemin, c => c.EmpreintePes6 = new string('a', 64));

        Assert.Null(erreur);
        var relu = ChargeurConfiguration.Charger(Chemin);
        Assert.Equal(EtatConfiguration.Valide, relu.Etat);
        Assert.Equal("Awa", relu.Configuration!.NomJoueur);
        Assert.Equal(new string('a', 64), relu.Configuration.EmpreintePes6);
    }

    [Fact]
    public void Empreinte_mal_formee_est_signalee()
    {
        File.WriteAllText(Chemin, """{ "nomJoueur": "Awa", "idReseauZeroTier": "633e31d8a269ea13", "empreintePes6": "1234" }""");
        Assert.Contains("64 caractères", Assert.Single(ChargeurConfiguration.Charger(Chemin).Problemes));
    }
}
