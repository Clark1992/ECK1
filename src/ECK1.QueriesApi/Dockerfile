FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["ECK1.QueriesAPI.csproj", "./"]

RUN dotnet restore "ECK1.QueriesAPI.csproj"

COPY . .

RUN dotnet publish "ECK1.QueriesAPI.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 80
EXPOSE 443

ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "ECK1.QueriesAPI.dll"]
