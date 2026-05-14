FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY services/notification-service/ services/notification-service/
RUN dotnet restore services/notification-service/notification-service.csproj
RUN dotnet publish services/notification-service/notification-service.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8086
ENTRYPOINT ["dotnet", "notification-service.dll"]
