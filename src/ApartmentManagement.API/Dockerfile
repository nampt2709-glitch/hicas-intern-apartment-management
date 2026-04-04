FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.sln ./
COPY src/ApartmentManagement.API/*.csproj src/ApartmentManagement.API/

RUN dotnet restore src/ApartmentManagement.API/ApartmentManagement.API.csproj

COPY . .
RUN dotnet publish src/ApartmentManagement.API/ApartmentManagement.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ApartmentManagement.API.dll"]
