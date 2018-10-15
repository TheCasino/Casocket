using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Casocket
{
    internal class Scheduler
    {
        private readonly Timer _timer;

        private ConcurrentQueue<Payload> _queue = new ConcurrentQueue<Payload>();

        public Scheduler()
        {
            _timer = new Timer(async _ =>
            {
                try
                {
                    if (!_queue.TryDequeue(out var payload)) return;
                    await HandlePayloadAsync(payload);
                    SetTimer();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }, null,
                TimeSpan.FromMilliseconds(-1),
                TimeSpan.FromMilliseconds(-1));
        }

        public void Enqueue(Payload payload)
        {
            _queue.Enqueue(payload);
            SetTimer();
        }

        private void SetTimer()
        {
            try
            {
                if (_queue.IsEmpty) return;

                var toRemove = _queue.Where(x => x.When < DateTimeOffset.UtcNow).ToArray();
                _queue = new ConcurrentQueue<Payload>(_queue.Where(x => x.When.ToUniversalTime() > DateTime.UtcNow).OrderBy(x => x.When));

                if (toRemove.Length > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        foreach (var item in toRemove)
                            await HandlePayloadAsync(item);
                    });
                }

                if (_queue.TryPeek(out var removeable))
                    _timer.Change(removeable.When.ToUniversalTime() - DateTime.UtcNow, TimeSpan.FromMilliseconds(-1));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public event Func<Payload, Task> Payload;

        private Task HandlePayloadAsync(Payload payload)
        {
            return Payload is null ? Task.CompletedTask : Payload.Invoke(payload);
        }
    }
}
