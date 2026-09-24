FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo
COPY QuizArena.sln Directory.Build.props ./
COPY src/QuizArena.Core/*.csproj src/QuizArena.Core/
COPY src/QuizArena.Infrastructure/*.csproj src/QuizArena.Infrastructure/
COPY src/QuizArena.Web/*.csproj src/QuizArena.Web/
RUN dotnet restore src/QuizArena.Web/QuizArena.Web.csproj
COPY src/ src/
RUN dotnet publish src/QuizArena.Web/QuizArena.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "QuizArena.Web.dll"]
