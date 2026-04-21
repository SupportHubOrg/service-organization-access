#Stage 1: Build and Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

#Restore dependencies
COPY ["src/OrganizationAccess/OrganizationAccess.csproj", "src/OrganizationAccess/"]
RUN dotnet restore "src/OrganizationAccess/OrganizationAccess.csproj"

#Copy the remaining source code and build
COPY . .
WORKDIR "/src/src/OrganizationAccess"
RUN dotnet publish "OrganizationAccess.csproj" -c Release -o /app/publish /p:UseAppHost=false

#Stage 2: Final Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

#Standard port for .NET applications running in containers
EXPOSE 8080 
ENTRYPOINT ["dotnet", "OrganizationAccess.dll"]