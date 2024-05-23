namespace Akka.IO.TcpTools.Standard
{
    public enum CloseCode : int
    {
        NormalClosure = 1000,
        GoingAway = 1001,
        ProtocolError = 1002

        // TODO: add the rest, https://datatracker.ietf.org/doc/html/rfc6455#section-7.4.1
    }
}
