# ======================
# Build stage
# ======================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution
COPY RESQ_Backend.sln ./

# Copy projects
COPY RESQ.Presentation/ RESQ.Presentation/
COPY RESQ.Application/ RESQ.Application/
COPY RESQ.Domain/ RESQ.Domain/
COPY RESQ.Infrastructure/ RESQ.Infrastructure/

# Restore
RUN dotnet restore RESQ.Presentation/RESQ.Presentation.csproj

# Publish
WORKDIR /src/RESQ.Presentation
RUN dotnet publish -c Release -o /app/publish

# ======================
# Runtime stage
# ======================
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RESQ.Presentation.dll"]
