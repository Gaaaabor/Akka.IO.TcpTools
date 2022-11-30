namespace Akka.IO.TcpTools
{
    public class FramedWebSocketMessage
    {
        private const int Offset = 8; // TODO: Calcualte this
        private readonly ulong _totalLength;
        private readonly MemoryStream _messageBytes;

        public FramedWebSocketMessage(ulong totalLength)
        {
            _totalLength = totalLength;
            _messageBytes = new MemoryStream();
        }

        public byte[] ReadAllBytes()
        {
            return _messageBytes.ToArray();
        }

        public void Write(ReadOnlySpan<byte> bytes)
        {
            _messageBytes.Write(bytes);
        }

        public void Close()
        {
            _messageBytes.Position = 0;
            _messageBytes?.Dispose();
        }

        public bool IsCompleted()
        {
            return (ulong)_messageBytes.Position >= (_totalLength + Offset);
        }
    }
}
