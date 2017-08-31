FROM microsoft/dotnet:2.0-sdk
MAINTAINER gwhite@fairwaytech.com
LABEL name "ap-cpt-web"
ARG source
WORKDIR /app
COPY out .
ENTRYPOINT ["dotnet", "ContentPackageTabulator.Web.dll"]