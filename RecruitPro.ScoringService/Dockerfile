FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore RecruitPro.ScoringService/RecruitPro.ScoringService.csproj

RUN dotnet publish RecruitPro.ScoringService/RecruitPro.ScoringService.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "RecruitPro.ScoringService.dll"]
