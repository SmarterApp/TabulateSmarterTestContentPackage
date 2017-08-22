FROM microsoft/dotnet:1.1-runtime
LABEL name "ap-cpt"
ARG source
WORKDIR /app
COPY out .
ENTRYPOINT ["dotnet", "ContentPackageTabulator.dll"]
