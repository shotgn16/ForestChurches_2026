using System.Threading.Channels;
using System.Text.Json;

namespace ForestChurches.Components.Logging
{
    public class SseManager
    {
        private readonly object _lock = new object();
        private readonly List<Channel<string>> _channels = new List<Channel<string>>();

        public ChannelReader<string> Subscribe(CancellationToken ct)
        {
            var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            lock (_lock)
            {
                _channels.Add(channel);
            }

            ct.Register(() =>
            {
                lock (_lock)
                {
                    _channels.Remove(channel);
                }
                channel.Writer.TryComplete();
            });

            return channel.Reader;
        }

        public async Task PublishAsync(object payload)
        {
            string json;
            try
            {
                json = JsonSerializer.Serialize(payload);
            }
            catch
            {
                json = "{}";
            }

            List<Channel<string>> copy;
            lock (_lock)
            {
                copy = _channels.ToList();
            }

            foreach (var ch in copy)
            {
                try
                {
                    ch.Writer.TryWrite(json);
                }
                catch
                {
                    // ignore
                }
            }

            await Task.CompletedTask;
        }
    }
}