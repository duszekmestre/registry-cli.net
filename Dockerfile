FROM mcr.microsoft.com/dotnet/sdk:5.0.103-alpine3.13 as publish

COPY ["/src/registry-cli", "/src"]
WORKDIR /src
RUN ls
RUN dotnet publish "registry-cli.csproj" -o /app

FROM mcr.microsoft.com/dotnet/runtime:5.0.3-alpine3.13 as final

WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["/app/registry-cli"]