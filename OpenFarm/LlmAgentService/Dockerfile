FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["LlmAgentService/LlmAgentService.csproj", "LlmAgentService/"]
COPY ["DatabaseAccess/DatabaseAccess.csproj", "DatabaseAccess/"]
RUN dotnet restore "LlmAgentService/LlmAgentService.csproj"
COPY . .
WORKDIR "/src/LlmAgentService"
RUN dotnet build "LlmAgentService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LlmAgentService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LlmAgentService.dll"]
