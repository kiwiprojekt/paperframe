FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["paperframe-server.csproj", "./"]
RUN dotnet restore "paperframe-server.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "./paperframe-server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./paperframe-server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
USER root
WORKDIR /app
COPY --from=publish /app/publish .
RUN mkdir -p /app/config && chown $APP_UID /app/config
USER $APP_UID
ENTRYPOINT ["dotnet", "paperframe-server.dll"]
