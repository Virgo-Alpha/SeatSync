FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SeatSync.Api/SeatSync.Api.csproj", "SeatSync.Api/"]
COPY ["SeatSync.Domain/SeatSync.Domain.csproj", "SeatSync.Domain/"]
COPY ["SeatSync.Infrastructure/SeatSync.Infrastructure.csproj", "SeatSync.Infrastructure/"]
RUN dotnet restore "SeatSync.Api/SeatSync.Api.csproj"
COPY . .
WORKDIR "/src/SeatSync.Api"

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SeatSync.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SeatSync.Api.dll"]
