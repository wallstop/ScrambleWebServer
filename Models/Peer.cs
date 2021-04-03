namespace ScrambleWebServer.Models
{
    using System;
    using System.Collections.Concurrent;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class Peer : IEquatable<Peer>, IDisposable
    {
        private static readonly Random Random = new Random();
        private static readonly ConcurrentDictionary<Peer, bool> Peers = new ConcurrentDictionary<Peer, bool>();

        public static int Count => Peers.Count;

        public readonly int id;
        public readonly WebSocket webSocket;

        public string lobby;
        public string name;

        public bool shouldCloseConnection = true;

        private Peer(int id, WebSocket webSocket)
        {
            this.id = id;
            this.webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                if (!shouldCloseConnection)
                {
                    return;
                }
                if (string.IsNullOrWhiteSpace(lobby))
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Have not joined any lobby yet",
                        CancellationToken.None);
                }
            });
        }

        public static Peer Create(WebSocket webSocket)
        {
            lock (Random)
            {
                Peer peer;
                do
                {
                    int peerId = Random.Next(int.MinValue, int.MaxValue);
                    peer = new Peer(peerId, webSocket);
                } while (!Peers.TryAdd(peer, true));

                return peer;
            }
        }

        public override int GetHashCode()
        {
            return id;
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is Peer other && Equals(other);
        }

        public bool Equals(Peer other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return id == other.id;
        }

        public override string ToString()
        {
            return id.ToString();
        }

        public void Dispose()
        {
            _ = Peers.TryRemove(this, out _);
        }
    }
}
