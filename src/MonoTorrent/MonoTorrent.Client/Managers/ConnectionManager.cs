//
// ConnectionManager.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Diagnostics;

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.Modes;
using MonoTorrent.Client.RateLimiters;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Main controller class for all incoming and outgoing connections
    /// </summary>
    public class ConnectionManager
    {
        struct AsyncConnectState
        {
            public AsyncConnectState (TorrentManager manager, IConnection connection, Stopwatch timer)
            {
                Manager = manager;
                Connection = connection;
                Timer = timer;
            }

            public IConnection Connection;
            public TorrentManager Manager;
            public Stopwatch Timer;
        }

        public event EventHandler<AttemptConnectionEventArgs> BanPeer;

        internal static readonly int ChunkLength = 2096 + 64;   // Download in 2kB chunks to allow for better rate limiting

        public DiskManager DiskManager { get; }

        public BEncodedString LocalPeerId { get; }

        /// <summary>
        /// The maximum number of half open connections
        /// </summary>
        public int MaxHalfOpenConnections => Settings.MaximumHalfOpenConnections;

        /// <summary>
        /// The maximum number of open connections
        /// </summary>
        public int MaxOpenConnections => Settings.MaximumConnections;

        /// <summary>
        /// The number of open connections
        /// </summary>
        public int OpenConnections
        {
            get
            {
                return (int) ClientEngine.MainLoop.QueueWait (() =>
                    (int) Toolbox.Accumulate (Torrents, (m) =>
                        m.Peers.ConnectedPeers.Count
                    )
                );
            }
        }

        List<AsyncConnectState> PendingConnects { get; }

        EngineSettings Settings { get; }

        LinkedList<TorrentManager> Torrents { get; set; }

        internal ConnectionManager(BEncodedString localPeerId, EngineSettings settings, DiskManager diskManager)
        {
            DiskManager = diskManager ?? throw new ArgumentNullException (nameof (diskManager));
            LocalPeerId = localPeerId ?? throw new ArgumentNullException (nameof (localPeerId));
            Settings = settings ?? throw new ArgumentNullException (nameof (settings));

            PendingConnects = new List<AsyncConnectState>();
            Torrents = new LinkedList<TorrentManager>();
        }

        internal void Add (TorrentManager manager)
            => Torrents.AddLast (manager);

        internal void Remove (TorrentManager manager)
            => Torrents.Remove (manager);

        internal async void ConnectToPeer(TorrentManager manager, Peer peer)
        {
            // Connect to the peer.
            IConnection connection = ConnectionFactory.Create(peer.ConnectionUri);
            if (connection == null)
                return;

            var state = new AsyncConnectState(manager, connection, Stopwatch.StartNew ());
            PendingConnects.Add(state);
            manager.Peers.ConnectingToPeers.Add(peer);

            bool succeeded;
            try {
                await NetworkIO.ConnectAsync (connection);
                succeeded = true;
            } catch {
                succeeded = false;
            }

            PendingConnects.Remove (state);
            manager.Peers.ConnectingToPeers.Remove (peer);
            if (manager.Engine == null || !manager.Mode.CanAcceptConnections) {
                connection.Dispose ();
                return;
            }

            try {
                if (!succeeded) {
                    peer.FailedConnectionAttempts++;
                    connection.Dispose ();
                    manager.Peers.BusyPeers.Add (peer);
                    manager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs (peer, ConnectionFailureReason.Unreachable, manager));
                } else {
                    PeerId id = new PeerId (peer, manager, connection);
                    id.LastMessageReceived.Restart ();
                    id.LastMessageSent.Restart ();

                    Logger.Log (id.Connection, "ConnectionManager - Connection opened");

                    ProcessNewOutgoingConnection (id);
                }
            } catch {
                // FIXME: Do nothing now?
            } finally {
                // Try to connect to another peer
                TryConnect ();
            }
        }


        internal async void ProcessNewOutgoingConnection (PeerId id)
        {
            // If we have too many open connections, close the connection
            if (OpenConnections > MaxOpenConnections) {
                CleanupSocket (id);
                return;
            }

            id.ProcessingQueue = true;
            id.TorrentManager.Peers.ActivePeers.Add(id.Peer);
            id.TorrentManager.Peers.HandshakingPeers.Add(id);

            try {
                // Create a handshake message to send to the peer
                var handshake = new HandshakeMessage(id.TorrentManager.InfoHash, LocalPeerId, VersionInfo.ProtocolStringV100);
                var result = await EncryptorFactory.CheckOutgoingConnectionAsync (id.Connection, id.Peer.AllowedEncryption, Settings, id.TorrentManager.InfoHash, handshake);
                id.Decryptor = result.Decryptor;
                id.Encryptor = result.Encryptor;
            } catch {
                // If an exception is thrown it's because we tried to establish an encrypted connection and something went wrong
                id.Peer.AllowedEncryption &= ~(EncryptionTypes.RC4Full | EncryptionTypes.RC4Header);

                id.TorrentManager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs(id.Peer, ConnectionFailureReason.EncryptionNegiotiationFailed, id.TorrentManager));
                CleanupSocket(id);
                return;
            }

            try {
                // Receive their handshake
                var handshake = await PeerIO.ReceiveHandshakeAsync (id.Connection, id.Decryptor);
                handshake.Handle(id);
            } catch {
                // If we choose plaintext and it resulted in the connection being closed, remove it from the list.
                id.Peer.AllowedEncryption &= ~id.EncryptionType;

                id.TorrentManager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs(id.Peer, ConnectionFailureReason.HandshakeFailed, id.TorrentManager));
                CleanupSocket(id);
                return;
            }

            try {
                id.TorrentManager.Peers.HandshakingPeers.Remove (id);
                id.TorrentManager.HandlePeerConnected(id);

                // If there are any pending messages, send them otherwise set the queue
                // processing as finished.
                if (id.QueueLength > 0)
                    ProcessQueue(id);
                else
                    id.ProcessingQueue = false;

                ReceiveMessagesAsync(id.Connection, id.Decryptor, id.TorrentManager.DownloadLimiter, id.Monitor, id.TorrentManager, id);

                id.WhenConnected.Restart ();
                id.LastBlockReceived.Restart ();
            } catch {
                id.TorrentManager.RaiseConnectionAttemptFailed (new ConnectionAttemptFailedEventArgs(id.Peer, ConnectionFailureReason.Unknown, id.TorrentManager));
                CleanupSocket(id);
                return;
            }
        }

        async void ReceiveMessagesAsync (IConnection connection, IEncryption decryptor, RateLimiterGroup downloadLimiter, ConnectionMonitor monitor, TorrentManager torrentManager, PeerId id)
        {
            try {
                while (true)
                {
                    var message = await PeerIO.ReceiveMessageAsync(connection, decryptor, downloadLimiter, monitor, torrentManager);
                    if (id.Disposed)
                    {
                        if (message is PieceMessage msg)
                            ClientEngine.BufferManager.FreeBuffer(msg.Data);
                    }
                    else
                    {
                        id.LastMessageReceived.Restart();
                        message.Handle(id);
                    }
                }
            } catch {
                CleanupSocket (id);
            }
        }

        internal void CleanupSocket(PeerId id)
        {
            if (id == null || id.Disposed) // Sometimes onEncryptoError will fire with a null id
                return;

            try
            {
                // We can reuse this peer if the connection says so and it's not marked as inactive
                bool canReuse = (id.Connection?.CanReconnect ?? false) && !id.TorrentManager.InactivePeerManager.InactivePeerList.Contains(id.Uri);
                id.TorrentManager.PieceManager.Picker.CancelRequests(id);
                id.Peer.CleanedUpCount++;

                if (id.PeerExchangeManager != null)
                    id.PeerExchangeManager.Dispose();

                if (!id.AmChoking)
                    id.TorrentManager.UploadingTo--;

                id.TorrentManager.Peers.ConnectedPeers.Remove (id);
                id.TorrentManager.Peers.HandshakingPeers.Remove (id);
                id.TorrentManager.Peers.ActivePeers.Remove(id.Peer);

                // If we get our own details, this check makes sure we don't try connecting to ourselves again
                if (canReuse && !LocalPeerId.Equals (id.Peer.PeerId))
                {
                    if (!id.TorrentManager.Peers.AvailablePeers.Contains(id.Peer) && id.Peer.CleanedUpCount < 5)
                        id.TorrentManager.Peers.AvailablePeers.Insert(0, id.Peer);
                }
            }
            catch(Exception ex)
            {
                Logger.Log(null, "CleanupSocket Error " + ex.Message);
            }
            finally
            {
                id.TorrentManager.RaisePeerDisconnected(new PeerDisconnectedEventArgs (id));
            }

            id.Dispose ();
        }

        internal void CancelPendingConnects()
        {
            CancelPendingConnects(null);
        }

        internal void CancelPendingConnects (TorrentManager manager)
        {
            foreach (var pending in PendingConnects)
                if (pending.Manager == manager || pending.Timer.ElapsedMilliseconds > 10 * 1000)
                    pending.Connection.Dispose ();
        }

        /// <summary>
        /// This method is called when the ClientEngine recieves a valid incoming connection
        /// </summary>
        /// <param name="id">The peer which just connected</param>
        internal void IncomingConnectionAccepted(PeerId id)
        {
            try
            {
                bool maxAlreadyOpen = OpenConnections >= Math.Min(this.MaxOpenConnections, id.TorrentManager.Settings.MaxConnections);
                if (LocalPeerId.Equals (id.Peer.PeerId) || maxAlreadyOpen)
                {
                    CleanupSocket (id);
                    return;
                }

                if (id.TorrentManager.Peers.ActivePeers.Contains(id.Peer))
                {
                    Logger.Log(id.Connection, "ConnectionManager - Already connected to peer");
                    id.Connection.Dispose();
                    return;
                }

                Logger.Log(id.Connection, "ConnectionManager - Incoming connection fully accepted");
                id.TorrentManager.Peers.AvailablePeers.Remove(id.Peer);
                id.TorrentManager.Peers.ActivePeers.Add(id.Peer);
                id.WhenConnected.Restart ();
                // Baseline the time the last block was received
                id.LastBlockReceived.Restart ();

                id.TorrentManager.HandlePeerConnected(id);

                // We've sent our handshake so begin our looping to receive incoming message
                ReceiveMessagesAsync (id.Connection, id.Decryptor, id.TorrentManager.DownloadLimiter, id.Monitor, id.TorrentManager, id);
            }
            catch
            {
                CleanupSocket (id);
            }
        }

        /// <param name="id">The peer whose message queue you want to start processing</param>
        internal async void ProcessQueue(PeerId id)
        {
            while (id.QueueLength > 0) {
                var msg = id.Dequeue ();
                var pm = msg as PieceMessage;

                try {
                    if (pm != null) {
                        pm.Data = ClientEngine.BufferManager.GetBuffer (pm.ByteLength);
                        try {
                            await DiskManager.ReadAsync (id.TorrentManager.Torrent, pm.StartOffset + ((long)pm.PieceIndex * id.TorrentManager.Torrent.PieceLength), pm.Data, pm.RequestLength);
                        } catch (Exception ex) {
                            id.TorrentManager.TrySetError (Reason.ReadFailure, ex);
                            return;
                        }
                        id.PiecesSent++;
                    }

                    await PeerIO.SendMessageAsync (id.Connection, id.Encryptor, msg, id.TorrentManager.UploadLimiter, id.Monitor, id.TorrentManager.Monitor);
                    if (msg is PieceMessage)
                        id.IsRequestingPiecesCount--;

                    id.LastMessageSent.Restart ();
                } catch {
                    CleanupSocket (id);
                    break;
                } finally {
                    if (pm?.Data != null)
                        ClientEngine.BufferManager.FreeBuffer (pm.Data);
                }
            }

            id.ProcessingQueue = false;
        }

        internal bool ShouldBanPeer(Peer peer)
        {
            if (BanPeer == null)
                return false;

            AttemptConnectionEventArgs e = new AttemptConnectionEventArgs(peer);
            BanPeer(this, e);
            return e.BanPeer;
        }

        internal void TryConnect()
        {
            // If we have already reached our max connections globally, don't try to connect to a new peer
            while (OpenConnections <= MaxOpenConnections && PendingConnects.Count <= MaxHalfOpenConnections) {
                var node = Torrents.First;
                while (node != null) {
                    // If we successfully connect, then break out of this loop and restart our
                    // connection process from the first node in the list again.
                    if (TryConnect(node.Value)) {
                        Torrents.Remove(node);
                        Torrents.AddLast(node);
                        break;
                    }

                    // If we did not successfully connect to a peer, then try the next torrent.
                    node = node.Next;
                }

                // If we failed to connect to anyone after walking the entire list, give up for now.
                if (node == null)
                    break;
            }
        }
        
        bool TryConnect (TorrentManager manager)
        {
            int i;
            Peer peer;
            if (!manager.Mode.CanAcceptConnections)
                return false;
            
            // If we have reached the max peers allowed for this torrent, don't connect to a new peer for this torrent
            if (manager.Peers.ConnectedPeers.Count >= manager.Settings.MaxConnections)
                return false;
            
            // If the torrent isn't active, don't connect to a peer for it
            if (!manager.Mode.CanAcceptConnections)
                return false;
            
            // If we are not seeding, we can connect to anyone. If we are seeding, we should only connect to a peer
            // if they are not a seeder.
            for (i = 0; i < manager.Peers.AvailablePeers.Count; i++)
                if (manager.Mode.ShouldConnect (manager.Peers.AvailablePeers[i]))
                    break;
            
            // If this is true, there were no peers in the available list to connect to.
            if (i == manager.Peers.AvailablePeers.Count)
                return false;
            
            // Remove the peer from the lists so we can start connecting to him
            peer = manager.Peers.AvailablePeers[i];
            manager.Peers.AvailablePeers.RemoveAt(i);

            if (ShouldBanPeer(peer))
                return false;
            
            // Connect to the peer
            ConnectToPeer(manager, peer);
            return true;
        }
    }
}
