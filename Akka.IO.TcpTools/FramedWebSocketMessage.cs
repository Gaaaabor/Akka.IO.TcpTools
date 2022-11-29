namespace Akka.IO.TcpTools
{
    internal class FramedWebSocketMessage
    {
        private const int Offset = 8; // TODO: Calcualte this
        private readonly ulong _totalLength;
        private readonly MemoryStream _messageBytes;

        internal FramedWebSocketMessage(ulong totalLength)
        {
            _totalLength = totalLength;
            _messageBytes = new MemoryStream();
        }

        internal byte[] ReadAllBytes()
        {
            return _messageBytes.ToArray();
        }

        internal void Write(ReadOnlySpan<byte> bytes)
        {
            _messageBytes.Write(bytes);
        }

        internal void Close()
        {
            _messageBytes.Position = 0;
            _messageBytes?.Dispose();
        }

        internal bool IsCompleted()
        {
            return (ulong)_messageBytes.Position >= (_totalLength + Offset);
        }
    }
}
