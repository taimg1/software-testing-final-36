FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/Feedback.Api/ src/Feedback.Api/
RUN dotnet publish src/Feedback.Api/Feedback.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Feedback.Api.dll"]
