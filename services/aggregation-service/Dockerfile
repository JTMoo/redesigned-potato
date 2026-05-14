FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY services/aggregation-service/ services/aggregation-service/
RUN dotnet restore services/aggregation-service/aggregation-service.csproj
RUN dotnet publish services/aggregation-service/aggregation-service.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8085
ENTRYPOINT ["dotnet", "aggregation-service.dll"]
