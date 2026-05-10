FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY ["server/src/Contry.Api/Contry.Api.csproj", "server/src/Contry.Api/"]
COPY ["server/src/Contry.Application/Contry.Application.csproj", "server/src/Contry.Application/"]
COPY ["server/src/Contry.Domain/Contry.Domain.csproj", "server/src/Contry.Domain/"]
COPY ["server/src/Contry.Infrastructure/Contry.Infrastructure.csproj", "server/src/Contry.Infrastructure/"]

RUN dotnet restore "server/src/Contry.Api/Contry.Api.csproj"

COPY . .

RUN dotnet publish "server/src/Contry.Api/Contry.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Contry.Api.dll"]
