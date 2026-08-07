# Contexto de build: a raiz do repositório (ver docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Restore isolado: copiar só os manifestos primeiro faz o layer de restore
# sobreviver a qualquer mudança de código.
COPY Directory.Build.props ./
COPY src/Jdice.Api/Jdice.Api.csproj src/Jdice.Api/
COPY src/Jdice.Application/Jdice.Application.csproj src/Jdice.Application/
COPY src/Jdice.Domain/Jdice.Domain.csproj src/Jdice.Domain/
COPY src/Jdice.Infrastructure/Jdice.Infrastructure.csproj src/Jdice.Infrastructure/
RUN dotnet restore src/Jdice.Api/Jdice.Api.csproj

COPY src/ src/
RUN dotnet publish src/Jdice.Api/Jdice.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Usuário sem privilégios já vem definido na imagem base do .NET.
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app .
ENTRYPOINT ["dotnet", "Jdice.Api.dll"]
