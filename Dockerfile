# Genisis API (ASP.NET Core 8) with the built React UI (src/Genisis.Api/wwwroot is committed), for Railway or any container host.
# Build:  docker build -t genisis .      Run:  docker run -p 8080:8080 -e Auth__JwtKey=... -e ConnectionStrings__MainDb=... genisis
# The settings are environment variables; docs/deployment-railway.md lists them.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Genisis.Api/Genisis.Api.csproj src/Genisis.Api/
RUN dotnet restore src/Genisis.Api/Genisis.Api.csproj
COPY src/Genisis.Api/ src/Genisis.Api/
RUN dotnet publish src/Genisis.Api/Genisis.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
# The Leaflet & Card PDF is drawn with system fonts, which the base image does not have.
RUN apt-get update && apt-get install -y --no-install-recommends fonts-dejavu-core && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
# Railway gives the port to listen on in PORT; anywhere else it is 8080.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet Genisis.Api.dll"]
