# ---- BUILD ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

RUN apt-get update && apt-get install -y libcap2-bin libsnappy1v5 && \
    ln -s /lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so && \
    ln -s /lib/x86_64-linux-gnu/libc.so.6 /usr/lib/x86_64-linux-gnu/libc.so && \
    rm -rf /var/lib/apt/lists/*

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

# Copy other
COPY src/Integration/*/*.csproj ./Integration/
RUN for file in $(ls Integration/*.csproj); do name=$(basename "$file" .csproj); mkdir -p src/Integration/${name}/ && mv $file src/Integration/${name}/; done

COPY src/Integration/CodeGen/*/*.csproj ./Integration/CodeGen/
RUN for file in $(ls Integration/CodeGen/*.csproj); do name=$(basename "$file" .csproj); mkdir -p src/Integration/CodeGen/${name}/ && mv $file src/Integration/CodeGen/${name}/; done

RUN dotnet restore ./src/Integration/ECK1.Integration.Cache.ShortTerm/ECK1.Integration.Cache.ShortTerm.csproj

COPY src/ ./

WORKDIR /src/Integration/ECK1.Integration.Cache.ShortTerm
RUN dotnet publish -c Release -o /app/publish

# ENTRYPOINT ["/bin/bash"]

# # ---- RUNTIME ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# RUN apt-get update && apt-get install -y procps && rm -rf /var/lib/apt/lists/*
# RUN apt-get update && apt-get install -y strace

COPY --from=build /app/publish .

EXPOSE 80
EXPOSE 443

ENTRYPOINT ["dotnet", "ECK1.Integration.Cache.ShortTerm.dll"]