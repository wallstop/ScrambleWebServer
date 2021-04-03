namespace ScrambleWebServer.Extension
{
    using System;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    public static class WebSocketExtensions
    {
        public static async Task SendTextAsync(this WebSocket webSocket, string message)
        {
            await webSocket.SendAsync(new ArraySegment<byte>($"{message}\n".GetBytes()), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
