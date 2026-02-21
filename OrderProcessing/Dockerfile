FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["OrderProcessing/OrderProcessing.csproj", "OrderProcessing/"]
COPY ["OrderProcessing.Application/OrderProcessing.Application.csproj", "OrderProcessing.Application/"]
COPY ["OrderProcessing.Infrastructure/OrderProcessing.Infrastructure.csproj", "OrderProcessing.Infrastructure/"]
COPY ["OrderProcessing.Domain/OrderProcessing.Domain.csproj", "OrderProcessing.Domain/"]
RUN dotnet restore "OrderProcessing/OrderProcessing.csproj"
COPY . .
WORKDIR "/src/OrderProcessing"
RUN dotnet build "./OrderProcessing.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./OrderProcessing.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderProcessing.dll"]
