FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/ shared/
COPY services/user-service/ services/user-service/
RUN dotnet restore services/user-service/user-service.csproj
RUN dotnet publish services/user-service/user-service.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8081
ENTRYPOINT ["dotnet", "user-service.dll"]
