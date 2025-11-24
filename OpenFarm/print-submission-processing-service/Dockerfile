FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["print-submission-processing-service/print-submission-processing-service.csproj", "print-submission-processing-service/"]
RUN dotnet restore "print-submission-processing-service/print-submission-processing-service.csproj"
COPY . .
WORKDIR "/src/print-submission-processing-service"
RUN dotnet build "print-submission-processing-service.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "print-submission-processing-service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "print-submission-processing-service.dll"]
