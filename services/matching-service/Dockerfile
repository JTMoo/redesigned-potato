FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY services/matching-service/ services/matching-service/
RUN dotnet restore services/matching-service/matching-service.csproj
RUN dotnet publish services/matching-service/matching-service.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8084
ENTRYPOINT ["dotnet", "matching-service.dll"]
