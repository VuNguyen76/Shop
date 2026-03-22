FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ClothingShop/ClothingShop.csproj", "ClothingShop/"]
RUN dotnet restore "ClothingShop/ClothingShop.csproj"
COPY . .
WORKDIR "/src/ClothingShop"
RUN dotnet publish "ClothingShop.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
# Optional: override at deploy time
# ENV ConnectionStrings__DefaultConnection="Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True;"
ENTRYPOINT ["dotnet", "ClothingShop.dll"]
