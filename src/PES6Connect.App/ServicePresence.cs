using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using PES6Connect.Core.Presence;
using PES6Connect.Core.Reseau;

namespace PES6Connect.App;

/// <summary>
/// Signale ce PC aux autres PES6 Connect du réseau ZeroTier et mesure leur ping.
/// - toutes les 3 s : annonce « je suis là » envoyée à tout le réseau (diffusion) ;
/// - toutes les 3 s : un ping envoyé à chaque joueur connu, qui répond aussitôt.
/// </summary>
internal sealed class ServicePresence : IDisposable
{
    private static readonly TimeSpan Intervalle = TimeSpan.FromSeconds(3);

    // Windows signale par erreur « connexion réinitialisée » sur un socket UDP quand un
    // message n'a pas pu être livré ; cette option le désactive (SIO_UDP_CONNRESET).
    private const int SioUdpConnReset = -1744830452;

    private readonly UdpClient _udp;
    private readonly ReseauIPv4 _reseau;
    private readonly string _monId;
    private readonly Func<(string Nom, string Empreinte)> _identite;
    private readonly CancellationTokenSource _arret = new();
    private readonly Stopwatch _horloge = Stopwatch.StartNew();

    private ServicePresence(UdpClient udp, ReseauIPv4 reseau, string monId, Func<(string, string)> identite)
    {
        _udp = udp;
        _reseau = reseau;
        _monId = monId;
        _identite = identite;
        Table = new TablePresence(monId);
    }

    public TablePresence Table { get; }
    public ReseauIPv4 Reseau => _reseau;
    public long Maintenant => _horloge.ElapsedMilliseconds;

    /// <param name="identite">Nom et empreinte de PES 6 à annoncer (relus à chaque annonce).</param>
    public static ServicePresence Demarrer(string monId, ReseauIPv4 reseau, Func<(string Nom, string Empreinte)> identite)
    {
        var udp = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            udp.EnableBroadcast = true;
            udp.Client.IOControl(SioUdpConnReset, [0, 0, 0, 0], null);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, MessagePresence.Port));
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            udp.Dispose();
            throw new ErreurZeroTier("PES6 Connect est peut-être déjà ouvert dans une autre fenêtre (port "
                + MessagePresence.Port + " occupé). Ferme-la puis clique sur « Tout revérifier ».");
        }
        catch (SocketException ex)
        {
            udp.Dispose();
            throw new ErreurZeroTier("Impossible d'écouter les autres joueurs : " + ex.Message);
        }

        var service = new ServicePresence(udp, reseau, monId, identite);
        _ = Task.Run(service.BoucleReceptionAsync);
        _ = Task.Run(service.BoucleEnvoiAsync);
        return service;
    }

    public void Dispose()
    {
        _arret.Cancel();
        _udp.Dispose();
        _arret.Dispose();
    }

    private async Task BoucleReceptionAsync()
    {
        while (!_arret.IsCancellationRequested)
        {
            UdpReceiveResult recu;
            try
            {
                recu = await _udp.ReceiveAsync(_arret.Token);
            }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { continue; }

            // On n'écoute que les PC de notre réseau ZeroTier.
            var source = recu.RemoteEndPoint.Address;
            if (!_reseau.Contient(source) || MessagePresence.Decoder(recu.Buffer) is not { } message)
            {
                continue;
            }

            switch (message.Type)
            {
                case TypeMessage.Annonce:
                    Table.RecevoirAnnonce(message, source, Maintenant);
                    break;
                case TypeMessage.Ping when message.Id != _monId:
                    await EnvoyerAsync(MessagePresence.Pong(_monId, message.Numero), recu.RemoteEndPoint);
                    break;
                case TypeMessage.Pong:
                    Table.RecevoirPong(message, Maintenant);
                    break;
            }
        }
    }

    private async Task BoucleEnvoiAsync()
    {
        var diffusion = new IPEndPoint(_reseau.Diffusion, MessagePresence.Port);
        while (!_arret.IsCancellationRequested)
        {
            var (nom, empreinte) = _identite();
            var annonce = MessagePresence.Annonce(_monId, MessagePresence.NettoyerNom(nom), empreinte, Table.MesPings(Maintenant));
            await EnvoyerAsync(annonce, diffusion);
            // Aussi en direct aux joueurs déjà connus : si la diffusion passe mal dans un sens,
            // il suffit qu'un des deux PC ait vu l'autre pour qu'ils se voient mutuellement.
            foreach (var joueur in Table.Joueurs(Maintenant))
            {
                await EnvoyerAsync(annonce, new IPEndPoint(joueur.Adresse, MessagePresence.Port));
            }
            foreach (var (adresse, numero) in Table.PreparerPings(Maintenant))
            {
                await EnvoyerAsync(MessagePresence.Ping(_monId, numero), new IPEndPoint(adresse, MessagePresence.Port));
            }

            try
            {
                await Task.Delay(Intervalle, _arret.Token);
            }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
        }
    }

    private async Task EnvoyerAsync(MessagePresence message, IPEndPoint destination)
    {
        try
        {
            var octets = message.Encoder();
            await _udp.SendAsync(octets, destination, _arret.Token);
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException or OperationCanceledException)
        {
            // Un message perdu n'est pas grave : le suivant part 3 secondes plus tard.
        }
    }
}
