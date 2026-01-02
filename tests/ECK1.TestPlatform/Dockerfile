# ---- BUILD ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

RUN --mount=type=secret,id=nugetconfig \
    cp /run/secrets/nugetconfig ./nuget.config

WORKDIR /src

COPY ./nuget.config ./*.props  ./

# Copy the main source project files
COPY src/*/*.csproj ./
RUN for file in $(ls *.csproj); do name=$(basename "$file" .csproj); mkdir -p src/${name}/ && mv $file src/${name}/; done

# Copy nugets if needed
COPY src/Nuget/*/*.csproj ./Nuget/
RUN for file in $(ls Nuget/*.csproj); do name=$(basename "$file" .csproj); mkdir -p src/Nuget/${name}/ && mv $file src/Nuget/${name}/; done

# Copy TestPlatform project file
COPY tests/ECK1.TestPlatform/ECK1.TestPlatform.csproj ./tests/ECK1.TestPlatform/

RUN dotnet restore ./tests/ECK1.TestPlatform/ECK1.TestPlatform.csproj

COPY src/ ./src/
COPY tests/ECK1.TestPlatform/ ./tests/ECK1.TestPlatform/

WORKDIR /src/tests/ECK1.TestPlatform
RUN dotnet publish -c Release -o /app/publish

# ---- RUNTIME ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 80
EXPOSE 443
ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "ECK1.TestPlatform.dll"]
