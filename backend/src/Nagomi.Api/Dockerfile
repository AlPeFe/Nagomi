FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY ["backend/src/Nagomi.Api/Nagomi.Api.csproj", "backend/src/Nagomi.Api/"]
RUN dotnet restore "backend/src/Nagomi.Api/Nagomi.Api.csproj"

COPY backend/src/Nagomi.Api/ backend/src/Nagomi.Api/
RUN dotnet publish "backend/src/Nagomi.Api/Nagomi.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
RUN apk add --no-cache wget \
    && addgroup -S nagomi \
    && adduser -S -G nagomi -h /app nagomi

COPY --from=build --chown=nagomi:nagomi /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
USER nagomi
ENTRYPOINT ["dotnet", "Nagomi.Api.dll"]
