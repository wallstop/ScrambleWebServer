namespace ScrambleWebServer
{
    using Extension;
    using Models;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class WebSocketHandler
    {
        private static int MaxPeers = 4096;
        private static int MaxLobbies = 1024;

        private static readonly Random Random = new Random();

        private readonly ConcurrentDictionary<Guid, Lobby> _lobbies = new ConcurrentDictionary<Guid, Lobby>();

        public async Task Handle(WebSocket webSocket)
        {
            if (MaxPeers <= Peer.Count)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Too many peers", CancellationToken.None);
                return;
            }

            using (Peer peer = Peer.Create(webSocket))
            {
                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024]);
                while (webSocket.State == WebSocketState.Open)
                {
                    List<byte> receivedBytes = new List<byte>();
                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    while (!result.EndOfMessage)
                    {
                        receivedBytes.AddRange(buffer);
                        result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    }

                    receivedBytes.AddRange(buffer.Take(result.Count));

                    string message = receivedBytes.ToArray().GetString();
                    await ParseMessage(peer, message);
                }

                if (peer.lobby.HasValue && _lobbies.TryGetValue(peer.lobby.Value, out Lobby lobby) &&
                    await lobby.Leave(peer))
                {
                    _ = _lobbies.TryRemove(peer.lobby.Value, out _);
                    Console.WriteLine("Deleted lobby {0}, {1} still open.", peer.lobby, _lobbies.Count);
                    peer.lobby = null;
                }

                peer.shouldCloseConnection = false;

                // TODO: Implement "OnClose"
            }
        }

        private async Task JoinLobby(Peer peer, Guid inputLobby)
        {
            Guid lobbyId = inputLobby;
            if (lobbyId == Guid.Empty)
            {
                if (MaxLobbies <= _lobbies.Count)
                {
                    Console.WriteLine("Max lobbies ({0}) exceeded.", _lobbies.Count);
                    return;
                }

                if (peer.lobby.HasValue)
                {
                    Console.WriteLine("Peer {0} already has a lobby.", peer);
                    return;
                }

                lobbyId = Guid.NewGuid();
                _lobbies[lobbyId] = new Lobby(lobbyId, peer.id);
                Console.WriteLine("Peer {0} created lobby {1}.", peer, lobbyId);
            }

            if (!_lobbies.TryGetValue(lobbyId, out Lobby lobby))
            {
                Console.WriteLine("Failed to find lobby for id {0}.", lobbyId);
                return;
            }

            if (lobby.Sealed)
            {
                Console.WriteLine("Lobby id {0} is already sealed.", lobbyId);
                return;
            }

            peer.lobby = lobbyId;
            List<Peer> peers = new List<Peer>();
            await lobby.IteratePeers(otherPeer => peers.Add(otherPeer));
            Console.WriteLine("Peer {0} is joining lobby {1} with {2} peers.", peer, lobbyId, string.Join(",", peers));
            await lobby.Join(peer);
            await peer.webSocket.SendTextAsync($"J: {lobbyId}");
        }

        private async Task ParseMessage(Peer peer, string message)
        {
            const int commandIndex = 3;
            int separaterIndex = message.IndexOf('\n');
            if (separaterIndex < 0)
            {
                Console.WriteLine("Invalid message received: {0}.", message);
                return;
            }

            string command = message.Substring(0, separaterIndex);
            if (command.Length < commandIndex)
            {
                Console.WriteLine("Invalid message received: {0}.", command);
                return;
            }

            string data = message.Substring(separaterIndex);
            if (IsCommand(command, 'J'))
            {
                string lobbyData = command.Substring(commandIndex).Trim();
                Guid lobbyId = Guid.Empty;
                if (!string.IsNullOrEmpty(lobbyData))
                {
                    lobbyId = Guid.Parse(lobbyData);
                }

                await JoinLobby(peer, lobbyId);
            }

            if (!peer.lobby.HasValue)
            {
                Console.WriteLine("Invalid command {0} while not in a lobby.", message);
                return;
            }

            if (!_lobbies.TryGetValue(peer.lobby.Value, out Lobby lobby))
            {
                Console.WriteLine("Server error, lobby {0} not found.", peer.lobby);
                return;
            }

            if (IsCommand(command, 'S'))
            {
                await lobby.Seal(peer);
                return;
            }


            int destinationId = int.Parse(command.Substring(commandIndex).Trim());
            if (destinationId == Lobby.HostId)
            {
                destinationId = lobby.hostId;
            }

            Peer destination = null;
            await lobby.IteratePeers(otherPeer =>
            {
                if (otherPeer.id == destinationId)
                {
                    destination = otherPeer;
                }
            });

            if (destination is null)
            {
                Console.WriteLine("Invalid destination {0}.", destinationId);
                return;
            }

            if (IsCommand(command, 'O') || IsCommand(command, 'A') || IsCommand(command, 'C'))
            {
                await destination.webSocket.SendTextAsync($"{command[0]}: {lobby.GetPeerId(peer)}{data}");
                return;
            }

            Console.WriteLine("Invalid command found in message {0}.", message);
            return;
        }

        private static bool IsCommand(string command, char commandKey)
        {
            return command.StartsWith($"{commandKey}: ");
        }
    }
}
