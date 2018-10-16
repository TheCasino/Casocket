using System;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Casocket
{
    public partial class CasocketClient
    {
        public Func<string, Task> MessageReceived;

        internal Task InternalMessageReceivedAsync(string message)
        {
            return MessageReceived is null ? Task.CompletedTask : MessageReceived.Invoke(message);
        }

        public Func<LogMessage, Task> Log;

        internal Task InternalLogAsync(LogMessage log)
        {
            return Log is null ? Task.CompletedTask : Log.Invoke(log);
        }

        public Func<WebSocketCloseStatus?, string, Task> SocketClosed;

        internal Task InternalSocketClosedAsync(WebSocketCloseStatus? status, string description)
        {
            return SocketClosed is null ? Task.CompletedTask : SocketClosed.Invoke(status, description);
        }
    }
}
