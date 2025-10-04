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

RUN dotnet restore ./src/ECK1.QueriesAPI/ECK1.QueriesAPI.csproj

COPY src/ ./

WORKDIR /src/ECK1.QueriesAPI
RUN dotnet publish -c Release -o /app/publish

# ENTRYPOINT ["/bin/bash"]

# # ---- RUNTIME ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 80
EXPOSE 443

ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "ECK1.QueriesAPI.dll"]