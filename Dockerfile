FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG SERVICE_NAME
WORKDIR /src

# Copia csproj e restaura dependências
COPY src/Shared/Shared.csproj src/Shared/
COPY src/${SERVICE_NAME}/${SERVICE_NAME}.csproj src/${SERVICE_NAME}/
RUN dotnet restore src/${SERVICE_NAME}/${SERVICE_NAME}.csproj

# Copia código e publica
COPY src/Shared/ src/Shared/
COPY src/${SERVICE_NAME}/ src/${SERVICE_NAME}/
RUN dotnet publish src/${SERVICE_NAME}/${SERVICE_NAME}.csproj -c Release -o /app

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
COPY certs/devcert.pfx /certs/devcert.pfx

ENV CERT_PATH=/certs/devcert.pfx
ENV CERT_PASSWORD=devpassword
ENV ASPNETCORE_ENVIRONMENT=Production
