FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["package/pkg_delegactor_library/pkg_delegactor_library/ConsoleDelegatorApplication/ConsoleDelegatorApplication.csproj", "ConsoleDelegatorApplication/"]
RUN dotnet restore "package/pkg_delegactor_library/pkg_delegactor_library/ConsoleDelegatorApplication/ConsoleDelegatorApplication.csproj"
COPY . .
WORKDIR "/src/ConsoleDelegatorApplication"
RUN dotnet build "ConsoleDelegatorApplication.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ConsoleDelegatorApplication.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConsoleDelegatorApplication.dll"]
