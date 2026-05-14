FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY services/api-gateway/ services/api-gateway/
RUN dotnet restore services/api-gateway/api-gateway.csproj
RUN dotnet publish services/api-gateway/api-gateway.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "api-gateway.dll"]
