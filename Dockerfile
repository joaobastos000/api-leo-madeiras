# Etapa 1: compila e publica a API com o SDK completo.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restaura os pacotes antes de copiar o código: enquanto o .csproj não muda, o Docker reaproveita esta camada.
COPY src/ApiFoundation/ApiFoundation.csproj src/ApiFoundation/
RUN dotnet restore src/ApiFoundation/ApiFoundation.csproj

COPY src/ApiFoundation/ src/ApiFoundation/
RUN dotnet publish src/ApiFoundation/ApiFoundation.csproj -c Release -o /app --no-restore

# Etapa 2: imagem final só com o runtime do ASP.NET Core (bem menor que a do SDK).
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ApiFoundation.dll"]
