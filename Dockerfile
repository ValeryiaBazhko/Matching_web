
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app


COPY MatchingService/MatchingService.csproj ./
RUN dotnet restore


COPY MatchingService/ ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 80
ENTRYPOINT ["dotnet", "MatchingService.dll"]