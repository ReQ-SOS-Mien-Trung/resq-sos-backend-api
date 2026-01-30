# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["RESQ.Presentation/RESQ.Presentation.csproj", "RESQ.Presentation/"]
COPY ["RESQ.Application/RESQ.Application.csproj", "RESQ.Application/"]
COPY ["RESQ.Infrastructure/RESQ.Infrastructure.csproj", "RESQ.Infrastructure/"]
COPY ["RESQ.Domain/RESQ.Domain.csproj", "RESQ.Domain/"]
RUN dotnet restore "RESQ.Presentation/RESQ.Presentation.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/RESQ.Presentation"
RUN dotnet build "RESQ.Presentation.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "RESQ.Presentation.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install PostgreSQL client for health checks (optional)
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "RESQ.Presentation.dll"]
