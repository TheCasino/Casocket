using System;

namespace Casocket
{
    public class Payload
    {
        public DateTimeOffset When { get; set; }
        public byte[] Data { get; set; }
    }
}
