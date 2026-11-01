namespace PES6Connect.Core.ZeroTier;

/// <summary>État du service Windows « ZeroTier One ».</summary>
public enum EtatService
{
    Absent,
    Arrete,
    EnTransition,
    Demarre,
    Inconnu,
}

/// <summary>Ce qu'on a constaté sur le PC (rempli par la partie Windows de l'application).</summary>
public sealed record ObservationZeroTier(EtatService Service, string? CheminExecutable, string? Version);

/// <summary>Interface qui permet de tester le diagnostic sans Windows.</summary>
public interface ISondeZeroTier
{
    ObservationZeroTier Observer();
}
