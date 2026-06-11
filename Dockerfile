FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Curatarr/Curatarr.csproj src/Curatarr/
RUN dotnet restore src/Curatarr/Curatarr.csproj

COPY src/Curatarr/ src/Curatarr/
RUN dotnet publish src/Curatarr/Curatarr.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

RUN mkdir -p /config && chown $APP_UID:$APP_UID /config
VOLUME ["/config"]

ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Curatarr="Data Source=/config/curatarr.db" \
    DataProtection__KeysPath=/config/data-protection-keys

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Curatarr.dll"]
