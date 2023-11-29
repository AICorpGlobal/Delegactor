FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["package/pkg_delegactor_library/pkg_delegactor_library/ClientApplication/ClientApplication.csproj", "ClientApplication/"]
RUN dotnet restore "package/pkg_delegactor_library/pkg_delegactor_library/ClientApplication/ClientApplication.csproj"
COPY . .
WORKDIR "/src/ClientApplication"
RUN dotnet build "ClientApplication.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ClientApplication.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ClientApplication.dll"]
