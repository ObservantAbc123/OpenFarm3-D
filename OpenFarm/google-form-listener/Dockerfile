FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["google-form-listener/google-form-listener.csproj", "google-form-listener/"]
RUN dotnet restore "google-form-listener/google-form-listener.csproj"
COPY . .
WORKDIR "/src/google-form-listener"
RUN dotnet build "google-form-listener.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "google-form-listener.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY /google-form-listener/${GOOGLE_SERVICE_ACCOUNT_CRED_FILE} .
ENTRYPOINT ["dotnet", "google-form-listener.dll"]
