using System;
using System.Data;

namespace Casocket
{
    public class ClientConfig
    {
        public Uri WebSocketAddress { get; set; }

        public int BufferSize { get; set; } = 1024;
        public int ReconnectAttemps { get; set; } = 5;

        public TimeSpan TimeOut { get; set; } = TimeSpan.FromSeconds(45);

        public ConnectionType ConnectionType { get; set; } = ConnectionType.Sequential;

        internal void VerifyConfig()
        {
            if(WebSocketAddress is null)
                throw new NoNullAllowedException("Uri must not be null");

            if(!WebSocketAddress.IsWellFormedOriginalString())
                throw new UriFormatException("Uri must be a well formed string");

            if(BufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(BufferSize));

            if(ReconnectAttemps < 0)
                throw new ArgumentOutOfRangeException(nameof(ReconnectAttemps));
        }
    }
}
