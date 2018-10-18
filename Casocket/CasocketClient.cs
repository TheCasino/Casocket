using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Casocket
{
    public partial class CasocketClient : IDisposable
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

        public async Task ConnectAsync()
        {
            do
            {
                if (_attempts == _config.ReconnectAttemps)
                {
                    await InternalLogAsync(new LogMessage
                    {
                        Type = LogType.Message,
                        Message = "Max amount of connection attemps made"
                    });
                    return;
                }

                try
                {
                    await _socket.ConnectAsync(_config.WebSocketAddress, CancellationToken.None).ConfigureAwait(false);
                }
                catch (WebSocketException exception)
                {
                    _attempts++;
                    await InternalLogAsync(new LogMessage
                    {
                        Type = LogType.Exception,
                        Message = exception.ToString()
                    });
                }

            } while (_socket.State == WebSocketState.None);

            _attempts = 0;

            Task.Run(async () => await StateMonitor().ConfigureAwait(false));

            var task = ListenAsync();

            if (_config.ConnectionType == ConnectionType.Sequential)
                await task.ConfigureAwait(false);
            else
                Task.Run(async () => await task.ConfigureAwait(false));
        }

        private async Task StateMonitor()
        {
            var lastState = WebSocketState.None;

            while (true)
            {
                if (_socket.State != lastState && _socket.State == WebSocketState.Closed)
                {
                    await InternalSocketClosedAsync(_socket.CloseStatus, _socket.CloseStatusDescription).ConfigureAwait(false);
                }

                lastState = _socket.State;

                await Task.Delay(100).ConfigureAwait(false);
            }
        }

        private async Task ListenAsync()
        {
            while (_socket.State == WebSocketState.Open)
            {
                try
                {
                    var buffer = new byte[_config.BufferSize];
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).ConfigureAwait(false);

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

                                await InternalMessageReceivedAsync(builder.ToString()).ConfigureAwait(false);
                            }

                            break;
                    }
                }
                catch (Exception exception)
                {
                    await InternalLogAsync(new LogMessage
                    {
                        Type = LogType.Exception,
                        Message = exception.ToString()
                    }).ConfigureAwait(false);
                }

                await Task.Delay(_config.Delay).ConfigureAwait(false);
            }
        }

        public async Task QueuePayloadAsync(Payload payload)
        {
            if (_socket.State != WebSocketState.Open)
            {
                await InternalLogAsync(new LogMessage
                {
                    Type = LogType.Message,
                    Message = "The websocket must be open to send a payload"
                }).ConfigureAwait(false);
                return;
            }

            _scheduler.Enqueue(payload);
        }

        private async Task SendAsync(Payload payload)
        {
            //TODO split this based on buffer
            try
            {
                var buffer = payload.Data;

                await _socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true,
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await InternalLogAsync(new LogMessage
                {
                    Type = LogType.Exception,
                    Message = exception.ToString()
                }).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            _socket?.Dispose();
        }
    }
}
