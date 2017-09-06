FROM microsoft/dotnet:2.0-sdk
LABEL name "ap-cpt"
ARG source
WORKDIR /app
COPY out .
ENTRYPOINT ["dotnet", "ContentPackageTabulator.dll"]
