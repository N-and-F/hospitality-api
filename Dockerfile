FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY NuGet.config ./
COPY Hospitality.Api.slnx ./
COPY Hospitality.Api/Hospitality.Api.csproj Hospitality.Api/
RUN dotnet restore Hospitality.Api/Hospitality.Api.csproj
COPY Hospitality.Api/ Hospitality.Api/
RUN dotnet publish Hospitality.Api/Hospitality.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "Hospitality.Api.dll"]
