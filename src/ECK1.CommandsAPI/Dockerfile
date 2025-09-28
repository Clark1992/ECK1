# ---- BUILD ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

RUN --mount=type=secret,id=nugetconfig \
    cp /run/secrets/nugetconfig ./nuget.config

WORKDIR /src

COPY ./nuget.config ./*.props  ./

# Copy the main source project files
COPY src/*/*.csproj ./
RUN for file in $(ls *.csproj); do mkdir -p src/${file%.*}/ && mv $file src/${file%.*}/; done

# Copy nugets if needed
COPY src/Nuget/*/*.csproj ./Nuget/
RUN for file in $(ls *.csproj); do mkdir -p src/Nuget/${file%.*}/ && mv $file src/Nuget/${file%.*}/; done

RUN dotnet restore ./src/ECK1.CommandsAPI/ECK1.CommandsAPI.csproj

COPY src/ ./

WORKDIR /src/ECK1.CommandsAPI
RUN dotnet publish -c Release -o /app/publish

# ---- RUNTIME ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 80
EXPOSE 443
ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "ECK1.CommandsAPI.dll"]
