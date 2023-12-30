ARG BASE_IMAGE=mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim
ARG BUILD_IMAGE=mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim
ARG BUILD_CONFIGURATION=Debug

FROM $BASE_IMAGE AS base
WORKDIR /app
EXPOSE 9002
EXPOSE 8081

FROM $BUILD_IMAGE AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Akka.IO.TcpTools.TestWebServer/Akka.IO.TcpTools.TestWebServer.csproj"
COPY . .
WORKDIR "/src/Akka.IO.TcpTools.TestWebServer"
ARG BUILD_CONFIGURATION
RUN echo "Building in $BUILD_CONFIGURATION mode"
RUN dotnet build --no-restore "/src/src/Akka.IO.TcpTools.TestWebServer/Akka.IO.TcpTools.TestWebServer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION
RUN echo "Publishing in $BUILD_CONFIGURATION mode"
RUN dotnet publish "/src/src/Akka.IO.TcpTools.TestWebServer/Akka.IO.TcpTools.TestWebServer.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM base AS final
EXPOSE 9002
EXPOSE 8081
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Akka.IO.TcpTools.TestWebServer.dll"]