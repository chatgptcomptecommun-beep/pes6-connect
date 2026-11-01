using PES6Connect.Core.ZeroTier;

namespace PES6Connect.Core.Tests;

public sealed class DiagnosticZeroTierTests
{
    private const string Exe = @"C:\ProgramData\ZeroTier\One\zerotier-one_x64.exe";

    [Fact]
    public void Service_demarre_est_ok_avec_version()
    {
        var r = DiagnosticZeroTier.Evaluer(new(EtatService.Demarre, Exe, "1.14.2"));
        Assert.Equal(NiveauVerification.Ok, r.Niveau);
        Assert.Equal(ActionProposee.Aucune, r.Action);
        Assert.Contains("1.14.2", r.Message);
    }

    [Fact]
    public void Service_arrete_propose_de_demarrer()
    {
        var r = DiagnosticZeroTier.Evaluer(new(EtatService.Arrete, Exe, null));
        Assert.Equal(NiveauVerification.Attention, r.Niveau);
        Assert.Equal(ActionProposee.DemarrerService, r.Action);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(Exe)]
    public void Service_absent_propose_d_installer(string? exe)
    {
        var r = DiagnosticZeroTier.Evaluer(new(EtatService.Absent, exe, null));
        Assert.Equal(NiveauVerification.Erreur, r.Niveau);
        Assert.Equal(ActionProposee.Installer, r.Action);
    }

    [Theory]
    [InlineData(EtatService.EnTransition)]
    [InlineData(EtatService.Inconnu)]
    public void Etats_incertains_ne_proposent_pas_d_action(EtatService etat)
    {
        var r = DiagnosticZeroTier.Evaluer(new(etat, null, null));
        Assert.Equal(NiveauVerification.Attention, r.Niveau);
        Assert.Equal(ActionProposee.Aucune, r.Action);
    }

    [Fact]
    public void Arguments_winget_sont_silencieux_et_exacts()
    {
        var args = PlanInstallationZeroTier.ArgumentsWinget();
        Assert.Equal("install", args[0]);
        Assert.Contains(PlanInstallationZeroTier.IdPaquetWinget, args);
        Assert.Contains("--exact", args);
        Assert.Contains("--silent", args);
    }

    [Theory]
    [InlineData(0, "terminée")]
    [InlineData(unchecked((int)0x8A150061), "déjà installé")]
    [InlineData(unchecked((int)0x8A150014), "catalogue")]
    [InlineData(-1, "0xFFFFFFFF")]
    public void Codes_winget_sont_traduits(int code, string extrait) =>
        Assert.Contains(extrait, PlanInstallationZeroTier.ExpliquerCodeSortie(code));
}
