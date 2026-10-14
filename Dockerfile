FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["src/VanPattenAIRouter.csproj", "src/"]
COPY ["tests/VanPattenAIRouter.Tests/VanPattenAIRouter.Tests.csproj", "tests/VanPattenAIRouter.Tests/"]
RUN dotnet restore "src/VanPattenAIRouter.csproj"
COPY . .
WORKDIR "/src/src"
RUN dotnet build "VanPattenAIRouter.csproj" -c Release -o /app/build

FROM build AS publish
WORKDIR "/src/src"
RUN dotnet publish "VanPattenAIRouter.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VanPattenAIRouter.dll"]