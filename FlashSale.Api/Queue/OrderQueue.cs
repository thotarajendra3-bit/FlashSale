using System.Threading.Channels;
using FlashSale.Api.DTOs;

namespace FlashSale.Api.Queue
{
    public class OrderQueue
    {
        private readonly Channel<OrderMessage> _channel;
        public OrderQueue()
        {
            var options = new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            };

            _channel = Channel.CreateBounded<OrderMessage>(options);
        }

        public async ValueTask EnqueueAsync(
            OrderMessage order,
            CancellationToken cancellationToken = default)
        {
            await _channel.Writer.WriteAsync(order, cancellationToken);
        }

        public IAsyncEnumerable<OrderMessage> ReadAllAsync(
            CancellationToken cancellationToken = default)
        {
            return _channel.Reader.ReadAllAsync(cancellationToken);
        }
    }
}
