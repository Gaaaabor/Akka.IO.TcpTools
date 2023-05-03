using Akka.Actor;
using Akka.Actor.Setup;
using Akka.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using System.Text;

namespace Akka.IO.TcpTools.Test.WebSocketConnectionTests
{
    public class WebSocketConnectionActorBaseTests : TestKit.Xunit2.TestKit
    {
        protected override void InitializeTest(ActorSystem system, ActorSystemSetup config, string actorSystemName, string testActorName)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            var serviceProvider = services.BuildServiceProvider();
            var dependencyResolverSetup = DependencyResolverSetup.Create(serviceProvider);
            config = config.And(dependencyResolverSetup);

            base.InitializeTest(system, config, actorSystemName ?? Guid.NewGuid().ToString("N"), testActorName);
        }

        [Theory]        
        [InlineData("Oh hai world")]
        [InlineData("987456159753210")]
        [InlineData("+!%//=()")]
        public async Task TestWebSocketConnectionActor_ShouldSuccessfullyReceiveTheSentMessage(string message)
        {
            const int port = 8081;

            Sys.ActorOf(DependencyResolver.For(Sys).Props<TestWebSocketConnectionManagerActor>(port, TestActor));

            using var clientWebSocket = new ClientWebSocket();
            await clientWebSocket.ConnectAsync(new Uri($"ws://localhost:{port}"), CancellationToken.None);
            
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await clientWebSocket.SendAsync(messageBytes, WebSocketMessageType.Text, true, CancellationToken.None);

            var receivedStringMessage = ExpectMsg<string>(TimeSpan.FromSeconds(10));
            Assert.Equal(message, receivedStringMessage);
        }

        [Fact]
        public async Task TestWebSocketConnectionActor_ShouldSuccessfullyReceiveTheLongMessage()
        {
            const string kickapoo = @"""Kickapoo""

[Jack Black (Narrator):]
A long ass fuckin' time ago
In a town called Kickapoo
There lived a humble family
Religious through and through
But, yeah, there was a black sheep
And he knew just what to do
His name was young J.B. and he refused to step in-line
A vision he did see of
Fuckin' rockin' all the time
He wrote a tasty jam and all the planets did align

[Jack Black (Son):]
Oh the dragons balls were blazin' as I stepped into his cave
Then I sliced his fuckin' cockles
With a long and shiny blade
'Twas I who fucked the dragon
Fuck a lie sing fuck a loo
And if you try to fuck with me
Then I shall fuck you too
Gotta get it on in the party zone
I gots to shoot a load in the party zone
Gotta lick a toad in the party zone
Gotta suck a chode in the party zone
[Crying]

[Meat Loaf (Father):]
You've disobeyed my orders, son
Why were you ever born?
Your brother's ten times better than you
Jesus loves him more
This music that you play for us comes from the depths of hell
Rock and roll's The Devil's work, he wants you to rebel
You become a mindless puppet
Beelzebub will pull the strings
Your heart will lose direction
And chaos it will bring
You better shut your mouth
You better watch your tone
You're grounded for a week with no telephone
Don't let me hear you cry
Don't let me hear you moan
You gotta praise The Lord when you're in my home

[Jack Black (Son):]
Dio can you hear me?
I am lost and so alone
I'm askin' for your guidance
Won't you come down from your throne?
I need a tight compadre who will teach me how to rock
My father thinks you're evil
But man, he can suck a cock
Rock is not The Devil's work
It's magical and rad
I'll never rock as long as I am stuck here with my dad

[Ronnie James Dio (Dio poster):]
I hear you brave young Jables
You are hungry for the rock
But to learn the ancient method
Sacred doors you must unlock
Escape your father's clutches
On this oppressive neighborhood
On a journey you must go
To find the land of Hollywood
In The City of Fallen Angels
Where the ocean meets the sand
You will form a strong alliance
And the world's most awesome band
To find your fame and fortune
Through the valley you must walk
You will face your inner demons
Now go my son and rock

[Jack Black (Narrator):]
So he bailed from fuckin' Kickapoo with hunger in his heart
And he journeyed far and wide to find the secrets of his art
But in the end he knew that he would find his counterpart
Rock. Rah-ha-ha-ha-hock. Raye-yayayayaye-yock";

            const int port = 8082;

            Sys.ActorOf(DependencyResolver.For(Sys).Props<TestWebSocketConnectionManagerActor>(port, TestActor));

            using var clientWebSocket = new ClientWebSocket();
            await clientWebSocket.ConnectAsync(new Uri($"ws://localhost:{port}"), CancellationToken.None);

            var messageBytes = Encoding.UTF8.GetBytes(kickapoo);
            await clientWebSocket.SendAsync(messageBytes, WebSocketMessageType.Text, true, CancellationToken.None);

            var receivedStringMessage = ExpectMsg<string>(TimeSpan.FromSeconds(10));
            Assert.Equal(kickapoo, receivedStringMessage);
        }
    }
}
