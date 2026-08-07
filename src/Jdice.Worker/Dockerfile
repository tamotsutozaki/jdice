# Contexto de build: a raiz do repositório (ver docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Jdice.Worker/Jdice.Worker.csproj src/Jdice.Worker/
COPY src/Jdice.Application/Jdice.Application.csproj src/Jdice.Application/
COPY src/Jdice.Domain/Jdice.Domain.csproj src/Jdice.Domain/
COPY src/Jdice.Infrastructure/Jdice.Infrastructure.csproj src/Jdice.Infrastructure/
RUN dotnet restore src/Jdice.Worker/Jdice.Worker.csproj

COPY src/ src/
RUN dotnet publish src/Jdice.Worker/Jdice.Worker.csproj -c Release -o /app --no-restore

# Imagem do ASP.NET Core, e não a de runtime puro: o Hangfire.AspNetCore traz
# dependência desse framework mesmo num worker sem servidor HTTP.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# O Npgsql tenta carregar as bibliotecas do Kerberos ao abrir conexão, mesmo
# quando a autenticação é por senha. Sem elas, a conexão funciona mas o log
# começa com um erro que não é erro — e ruído assim faz perder tempo quando
# houver um problema de verdade.
RUN apk add --no-cache krb5-libs

USER $APP_UID

COPY --from=build /app .
ENTRYPOINT ["dotnet", "Jdice.Worker.dll"]
