using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Casocket
{
    public class CasocketClient
    {
        private readonly ClientConfig _config;
        private readonly ClientWebSocket _socket;
        private readonly UTF8Encoding _encoder;

        private readonly ConcurrentQueue<byte[]> _bufferQueue;

        public CasocketClient(ClientConfig config)
        { 
            _config = config;
            _socket = new ClientWebSocket();
            _encoder = new UTF8Encoding();
            _bufferQueue = new ConcurrentQueue<byte[]>();
        }

        public Func<WebSocketMessage, Task> MessageReceived;

        internal Task InternalMessageReceived(WebSocketMessage message)
        {
            return MessageReceived is null ? Task.CompletedTask : MessageReceived.Invoke(message);
        }

        public async Task ConnectAsync()
        {
            await _socket.ConnectAsync(_config.WebSocketAddress, CancellationToken.None);
            var tasks = Task.WhenAll(ListenAsync(), SendAsync());

            if (_config.ConnectionType == ConnectionType.Sequential)
                await tasks;

            Task.Run(async () => await tasks);
        }

        private async Task ListenAsync()
        {
            while (_socket.State == WebSocketState.Open)
            {
                var buffer = new byte[_config.BufferSize];
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                switch (result.MessageType)
                {
                    case WebSocketMessageType.Text:
                        var trimmedBuffer = buffer.TrimEnd(result.Count);

                        _bufferQueue.Enqueue(trimmedBuffer);

                        if (result.EndOfMessage)
                        {
                            var builder = new StringBuilder();

                            //no idea if this is the best/right way to do this
                            while (_bufferQueue.TryDequeue(out var qBuffer))
                            {
                                builder.Append(_encoder.GetString(qBuffer));
                            }

                            var message = new WebSocketMessage
                            {
                                Message = builder.ToString()
                            };

                            await InternalMessageReceived(message);
                        }
                        break;
                }
            }
        }

        private async Task SendAsync()
        {

        }
    }
}
