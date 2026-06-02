FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.sln .
COPY ExamHub.Core/*.csproj ExamHub.Core/
COPY ExamHub.Infrastructure/*.csproj ExamHub.Infrastructure/
COPY ExamHub.Web/*.csproj ExamHub.Web/
RUN dotnet restore ExamHub.Web/ExamHub.Web.csproj
COPY . .
RUN dotnet publish ExamHub.Web/ExamHub.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ExamHub.Web.dll"]
