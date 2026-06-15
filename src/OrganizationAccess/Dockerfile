#Stage 1: Base runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
EXPOSE 8080
WORKDIR /app

#Stage 2: Build environment
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

#Restore dependencies
COPY ["OrganizationAccess.csproj", "."]
RUN dotnet restore "./OrganizationAccess.csproj"

#Copy the remaining source code and build
COPY . .
RUN dotnet build "./OrganizationAccess.csproj" -c $BUILD_CONFIGURATION -o /app/build

#Stage 3: Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OrganizationAccess.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

#Stage 4: Final output image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

#Ensure the non-root user has access to the Logs directory defined in appsettings.json
USER root
RUN mkdir -p /app/Logs && chown -R $APP_UID:$APP_UID /app/Logs

#Switch back to the restricted user for security
USER $APP_UID

ENTRYPOINT ["dotnet", "OrganizationAccess.dll"]