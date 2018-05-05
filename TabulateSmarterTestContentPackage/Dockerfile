FROM microsoft/dotnet:2.0-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.0-sdk AS build
WORKDIR /src
COPY . .
RUN dotnet restore -nowarn:msb3202,nu1503

WORKDIR /src
RUN dotnet build -c Release -o /app

FROM build AS publish
RUN dotnet publish -c Release -o /app


#build the app image
FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENV path-to-data=""
CMD ["dotnet", "TabulateSmarterTestContentPackage.dll", "-j", "$path-to-data"]


#example usage for CI
#docker build "path/to/directory/containing/Dockerfile" -t tabulator-v-1-xx-xx
#docker run -v /path/to/data:/path/to/data:rw -e path-to-data="/path/to/data/item-xxx-xxx" --name tabulator-v-1-xx-xx container-item-xxx-xxx