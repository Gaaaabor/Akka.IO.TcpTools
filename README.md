# Akka.IO.TcpTools

This package is created in order to make WebSocket message receiving / sending easier in the Akka ActorSystem.

The WebSocketConnectionActorBase class can be used as a WebSocketClient.
It decodes received messages, can receive framed messages and have the possibility to response to Ping/Pong messages if the OnPingReceivedAsync / OnPongReceivedAsync methods ovewritten.