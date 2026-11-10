# ===================================================================
# Dockerfile cho Backend ASP.NET Core 8.0 - Game 3D Cờ Cá Ngựa
# Dùng để deploy trực tiếp lên Render, Railway, Fly.io hoặc Docker VPS
# ===================================================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["CoCaNgua.Server.csproj", "./"]
RUN dotnet restore "CoCaNgua.Server.csproj"

COPY . .
RUN dotnet publish "CoCaNgua.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=5001
EXPOSE 5001

ENTRYPOINT ["dotnet", "CoCaNgua.Server.dll"]
