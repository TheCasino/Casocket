using System;

namespace Casocket
{
    public class LogMessage
    {
        public LogType Type { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            var time = DateTime.UtcNow;
            var niceTime = $"{(time.Hour < 10 ? "0" : "")}{time.Hour}:{(time.Minute < 10 ? "0" : "")}{time.Minute}" +
                           $":{(time.Second < 10 ? "0" : "")}{time.Second}";

            return $"[{niceTime}] [{Type,-9}] {Message}";
        }
    }
}
