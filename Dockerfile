# ===================================================================
# Dockerfile cho Backend ASP.NET Core 8.0 - Game 3D Cờ Cá Ngựa
# Tương thích mặc định với Render, Railway, Fly.io (Root Context)
# ===================================================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["Backend/CoCaNgua.Server.csproj", "Backend/"]
RUN dotnet restore "Backend/CoCaNgua.Server.csproj"

# Copy Backend source code only
COPY Backend/ Backend/
WORKDIR "/src/Backend"
RUN dotnet publish "CoCaNgua.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=5001
EXPOSE 5001

ENTRYPOINT ["dotnet", "CoCaNgua.Server.dll"]
