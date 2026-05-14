FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY services/receipt-service/ services/receipt-service/
RUN dotnet restore services/receipt-service/receipt-service.csproj
RUN dotnet publish services/receipt-service/receipt-service.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8082
ENTRYPOINT ["dotnet", "receipt-service.dll"]
