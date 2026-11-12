# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["src/VanPattenAIRouter.csproj", "src/"]
RUN dotnet restore "src/VanPattenAIRouter.csproj"
COPY . .
WORKDIR "/src/src"
RUN dotnet publish "VanPattenAIRouter.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/data
EXPOSE 8080
ENTRYPOINT ["dotnet", "VanPattenAIRouter.dll"]
