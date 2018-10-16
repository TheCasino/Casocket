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
        private readonly Scheduler _scheduler;

        private readonly ConcurrentQueue<byte[]> _bufferQueue;
        
        private int _attempts;

        public CasocketClient(ClientConfig config)
        { 
            _config = config;
            _socket = new ClientWebSocket();
            _encoder = new UTF8Encoding();
            _scheduler = new Scheduler();
            _scheduler.Payload += SendAsync;
            _bufferQueue = new ConcurrentQueue<byte[]>();
        }

        public Func<string, Task> MessageReceived;

        internal Task InternalMessageReceivedAsync(string message)
        {
            return MessageReceived is null ? Task.CompletedTask : MessageReceived.Invoke(message);
        }

        public Func<string, Task> Log;

        internal Task InternalLogAsync(string log)
        {
            return Log is null ? Task.CompletedTask : Log.Invoke(log);
        }

        public async Task ConnectAsync()
        {
            do
            {
                if (_attempts == _config.ReconnectAttemps)
                {
                    await InternalLogAsync("Max number of connection attemps made");
                    return;
                }

                try
                {
                    await _socket.ConnectAsync(_config.WebSocketAddress, CancellationToken.None);
                }
                catch (WebSocketException exception)
                {
                    _attempts++;
                    await InternalLogAsync(exception.ToString());
                }

            } while (_socket.State == WebSocketState.None);

            _attempts = 0;

            var task = ListenAsync();

            if (_config.ConnectionType == ConnectionType.Sequential)
                await task;
            else
                Task.Run(async () => await task);
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
                            
                            while (_bufferQueue.TryDequeue(out var qBuffer))
                            {
                                builder.Append(_encoder.GetString(qBuffer));
                            }

                            await InternalMessageReceivedAsync(builder.ToString());
                        }
                        break;
                }

                await Task.Delay(_config.Delay);
            }
        }

        public void QueuePayload(Payload payload)
        {
            _scheduler.Enqueue(payload);
        }

        private async Task SendAsync(Payload payload)
        {
            var buffer = payload.Data;

            await _socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true,
                CancellationToken.None);
        }
    }
}
