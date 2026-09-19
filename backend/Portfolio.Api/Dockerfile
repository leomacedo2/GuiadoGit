FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/Portfolio.Api/Portfolio.Api.csproj backend/Portfolio.Api/
RUN dotnet restore backend/Portfolio.Api/Portfolio.Api.csproj
COPY backend/Portfolio.Api/ backend/Portfolio.Api/
RUN dotnet publish backend/Portfolio.Api/Portfolio.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Portfolio.Api.dll"]
