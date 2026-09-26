# === 1. asama: derleme (SDK imaji buyuk, sadece build icin) ===
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Once sadece proje dosyalarini kopyalayip restore: kod degisse de paketler degismediyse
# Docker bu katmani onbellekten kullanir, her build'de NuGet indirmesi yapilmaz.
COPY StayFlow.slnx ./
COPY StayFlow.Domain/StayFlow.Domain.csproj StayFlow.Domain/
COPY StayFlow.Application/StayFlow.Application.csproj StayFlow.Application/
COPY StayFlow.Infrastructure/StayFlow.Infrastructure.csproj StayFlow.Infrastructure/
COPY StayFlow.API/StayFlow.API.csproj StayFlow.API/
RUN dotnet restore StayFlow.API/StayFlow.API.csproj

COPY . .
RUN dotnet publish StayFlow.API/StayFlow.API.csproj -c Release -o /app/publish --no-restore

# === 2. asama: calisma zamani (sadece ASP.NET runtime - kucuk ve daha az saldiri yuzeyi) ===
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# .NET imajlarindaki hazir yetkisiz kullanici: container root olarak calismasin.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "StayFlow.API.dll"]
