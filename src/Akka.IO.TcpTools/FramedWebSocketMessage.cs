namespace Akka.IO.TcpTools
{
    /// <summary>
    /// This simple class is used to hold the received frames of a framed message, track the total length and indicate if the full message was received.
    /// </summary>
    public class FramedWebSocketMessage
    {
        private const int Offset = 8; // TODO: Calcualte this, this is hacky...
        private readonly ulong _totalLength;
        private readonly MemoryStream _messageBytes;

        public FramedWebSocketMessage(ulong totalLength)
        {
            _totalLength = totalLength;
            _messageBytes = new MemoryStream();
        }

        /// <summary>
        /// Returns all bytes from the underlying stream.
        /// </summary>
        /// <returns></returns>
        public byte[] ReadAllBytes()
        {
            return _messageBytes.ToArray();
        }

        /// <summary>
        /// Write the provided bytes into the underlying stream.
        /// </summary>
        /// <param name="bytes">The bytes to write</param>
        public void Write(ReadOnlySpan<byte> bytes)
        {
            _messageBytes.Write(bytes);
        }

        /// <summary>
        /// Closes the underlying stream.
        /// </summary>
        public void Close()
        {
            _messageBytes.Position = 0;
            _messageBytes?.Dispose();
        }

        /// <summary>
        /// Returns true when the full message is written.
        /// </summary>
        /// <returns>True or false</returns>
        public bool IsCompleted()
        {
            return (ulong)_messageBytes.Position >= (_totalLength + Offset);
        }
    }
}
