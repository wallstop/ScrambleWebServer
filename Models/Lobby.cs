namespace ScrambleWebServer.Models
{
    using Extension;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class Lobby
    {
        public const int HostId = 1;

        public readonly Guid id;
        public readonly int hostId;

        public bool Sealed => _sealed;

        private readonly List<Peer> _peers = new List<Peer>();
        private bool _sealed = false;

        private volatile bool _shouldClose = true;
        private Task _closeTask = null;

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);

        public Lobby(Guid id, int hostId)
        {
            this.id = id != Guid.Empty ? id : throw new ArgumentException(nameof(id));
            this.hostId = hostId;
        }

        public int GetPeerId(Peer peer)
        {
            if (peer is null)
            {
                throw new ArgumentNullException(nameof(peer));
            }

            if (hostId == peer.id)
            {
                return HostId;
            }

            return peer.id;
        }

        public async Task IteratePeers(Action<Peer> peerAction)
        {
            using (await _lock.WaitAsyncWithAutoRelease())
            {
                foreach (Peer peer in _peers)
                {
                    peerAction(peer);
                }
            }
        }

        public async Task Join(Peer peer)
        {
            if (peer is null)
            {
                throw new ArgumentNullException(nameof(peer));
            }

            using (await _lock.WaitAsyncWithAutoRelease())
            {
                int peerId = GetPeerId(peer);
                string joined = $"I: {peerId}";
                await peer.webSocket.SendTextAsync(joined);

                foreach (Peer otherPeer in _peers)
                {
                    await otherPeer.webSocket.SendTextAsync($"N: {peerId}");
                    await peer.webSocket.SendTextAsync($"N: {otherPeer.id}");
                }

                _peers.Add(peer);
            }
        }

        public async Task<bool> Leave(Peer peer)
        {
            if (peer is null)
            {
                throw new ArgumentNullException(nameof(peer));
            }

            using (await _lock.WaitAsyncWithAutoRelease())
            {
                if (!_peers.Contains(peer))
                {
                    return false;
                }

                int peerId = GetPeerId(peer);
                bool close = peerId == HostId;

                List<Task> leaveTasks = _peers.Select(otherPeer =>
                {
                    if (close)
                    {
                        return otherPeer.webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            "Room host has disconnected", CancellationToken.None);
                    }

                    return otherPeer.webSocket.SendTextAsync($"D: {peerId}");
                }).ToList();
                await Task.WhenAll(leaveTasks);

                _ = _peers.Remove(peer);

                if (close && _closeTask != null)
                {
                    _shouldClose = false;
                }

                return close;
            }
        }

        public async Task Seal(Peer peer)
        {
            if (peer is null)
            {
                throw new ArgumentNullException(nameof(peer));
            }

            if (GetPeerId(peer) != HostId)
            {
                throw new ArgumentException(nameof(peer));
            }

            using (await _lock.WaitAsyncWithAutoRelease())
            {
                _sealed = true;

                foreach (Peer otherPeer in _peers)
                {
                    await otherPeer.webSocket.SendTextAsync("S: ");
                }

                _closeTask = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    if (!_shouldClose)
                    {
                        return;
                    }
                    using (await _lock.WaitAsyncWithAutoRelease())
                    {
                        List<Task> closeTasks = _peers.Select(otherPeer =>
                            otherPeer.webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Seal complete",
                                CancellationToken.None)).ToList();
                        await Task.WhenAll(closeTasks);
                    }

                    _closeTask = null;
                });
            }
        }
    }
}
