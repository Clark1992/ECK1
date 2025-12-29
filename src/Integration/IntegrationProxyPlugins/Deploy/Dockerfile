# ---- BUILD ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

ARG PLUGIN_PROJ

COPY ./nuget.config ./*.props  ./

COPY src/Integration/IntegrationProxyPlugins/ECK1.Integration.Plugin.Abstractions/*.csproj ./Integration/IntegrationProxyPlugins/
COPY src/Integration/IntegrationProxyPlugins/$PLUGIN_PROJ/*.csproj ./Integration/IntegrationProxyPlugins/

RUN for file in $(ls Integration/IntegrationProxyPlugins/*.csproj); do name=$(basename "$file" .csproj); mkdir -p src/Integration/IntegrationProxyPlugins/${name}/ && mv $file src/Integration/IntegrationProxyPlugins/${name}/; done

# TODO: remove when will target nuget
COPY src/Nuget/*/*.csproj ./Nuget/
RUN for file in $(ls Nuget/*.csproj); do name=$(basename "$file" .csproj); mkdir -p src/Nuget/${name}/ && mv $file src/Nuget/${name}/; done

RUN dotnet restore src/Integration/IntegrationProxyPlugins/$PLUGIN_PROJ/$PLUGIN_PROJ.csproj

COPY src/Integration/IntegrationProxyPlugins/ECK1.Integration.Plugin.Abstractions ./src/Integration/IntegrationProxyPlugins/ECK1.Integration.Plugin.Abstractions
COPY src/Integration/IntegrationProxyPlugins/$PLUGIN_PROJ ./src/Integration/IntegrationProxyPlugins/$PLUGIN_PROJ
COPY src/Integration/CodeGen/ECK1.Integration.Plugin.Abstractions.CodeGen ./src/Integration/CodeGen/ECK1.Integration.Plugin.Abstractions.CodeGen
COPY src/Integration/CodeGen/ECK1.CodeGen.Shared ./src/Integration/CodeGen/ECK1.CodeGen.Shared

WORKDIR /src/src/Integration/IntegrationProxyPlugins/$PLUGIN_PROJ
RUN dotnet publish -c Release -o /lib/publish

# CMD ["tail", "-f", "/dev/null"]

# ---- RUNTIME ----
FROM busybox:stable AS final
WORKDIR /lib

ARG PLUGIN
ENV PLUGIN=$PLUGIN

COPY --from=build /lib/publish .

# CMD ["sh", "-c", "mkdir -p /mnt/plugins/$PLUGIN && cp -f /lib/*.dll /mnt/plugins/$PLUGIN && echo '✅ Plugin copied' && exit 0"]

CMD ["sh", "-c", " \
    mkdir -p /mnt/plugins/$PLUGIN && \
    cp -f /lib/*.dll /mnt/plugins/$PLUGIN && \
    chown -R 1000:1000 /mnt/plugins/$PLUGIN && \
    chmod -R 755 /mnt/plugins/$PLUGIN && \
    echo '✅ Plugin copied & permissions set' && \
    exit 0 \
"]
