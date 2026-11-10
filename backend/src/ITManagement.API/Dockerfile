FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/ITManagement.API/ITManagement.API.csproj", "src/ITManagement.API/"]
COPY ["src/ITManagement.Application/ITManagement.Application.csproj", "src/ITManagement.Application/"]
COPY ["src/ITManagement.Domain/ITManagement.Domain.csproj", "src/ITManagement.Domain/"]
COPY ["src/ITManagement.Infrastructure/ITManagement.Infrastructure.csproj", "src/ITManagement.Infrastructure/"]
RUN dotnet restore "src/ITManagement.API/ITManagement.API.csproj"

COPY . .
WORKDIR "/src/src/ITManagement.API"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
# Volume cho file storage — không được expose ra public
VOLUME ["/app/storage"]
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ITManagement.API.dll"]
