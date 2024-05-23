namespace Akka.IO.TcpTools.Standard
{
    /// <summary>
    /// Defines the interpretation of the "Payload data".
    /// </summary>
    public enum StandardMessageType : byte
    {
        /// <summary>
        /// %x0 denotes a continuation frame.
        /// </summary>
        Continuation = 0x0,

        /// <summary>
        /// %x1 denotes a text frame.
        /// </summary>
        Text = 0x1,

        /// <summary>
        /// %x2 denotes a binary frame.
        /// </summary>
        Binary = 0x2,

        /// <summary>
        /// %x8 denotes a connection close.
        /// </summary>
        Close = 0x8,

        /// <summary>
        /// %x9 denotes a ping.
        /// </summary>
        Ping = 0x9,

        /// <summary>
        /// %xA denotes a pong.
        /// </summary>
        Pong = 0xA,

        /// <summary>
        /// All other non specified: <br/>
        /// %x3-7 are reserved for further non-control frames.<br/>
        /// %xB-F are reserved for further control frames.<br/>
        /// </summary>
        Invalid = 255
    }
}
