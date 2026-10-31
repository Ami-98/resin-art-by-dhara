# Stage 1: Build React SPA into static assets
FROM node:20-alpine AS client-build
WORKDIR /src/client
COPY client/package*.json ./
RUN npm install
COPY client/ ./
RUN npm run build

# Stage 2: Build & publish ASP.NET Core API
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS server-build
WORKDIR /src
COPY server/ResinArt.Api/ResinArt.Api.csproj server/ResinArt.Api/
RUN dotnet restore server/ResinArt.Api/ResinArt.Api.csproj
COPY server/ResinArt.Api/ server/ResinArt.Api/
COPY --from=client-build /src/server/ResinArt.Api/wwwroot server/ResinArt.Api/wwwroot
RUN dotnet publish server/ResinArt.Api/ResinArt.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime image (mount persistent volume at /var/resinart-data)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=server-build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    Storage__RootPath=/var/resinart-data \
    Storage__SeedSampleData=true
VOLUME ["/var/resinart-data"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "ResinArt.Api.dll"]
