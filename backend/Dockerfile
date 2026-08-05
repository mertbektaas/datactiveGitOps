# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["backend.csproj", "./"]
RUN dotnet restore "backend.csproj"

# Copy remaining source code and publish release build
COPY . .
RUN dotnet publish "backend.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Environment
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Configure ASP.NET Core environment variables for port 8080
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Copy compiled binaries from build stage
COPY --from=build /app/publish .

# Define container entrypoint
ENTRYPOINT ["dotnet", "backend.dll"]
