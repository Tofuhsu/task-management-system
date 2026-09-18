FROM node:24-alpine AS frontend-build
WORKDIR /src/frontend

COPY frontend/task-manager-web/package.json frontend/task-manager-web/package-lock.json ./
RUN npm ci

COPY frontend/task-manager-web/ ./
ARG VITE_API_BASE_URL=/api
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src

COPY backend/TaskManager.Api/TaskManager.Api.csproj backend/TaskManager.Api/
RUN dotnet restore backend/TaskManager.Api/TaskManager.Api.csproj

COPY backend/TaskManager.Api/ backend/TaskManager.Api/
RUN dotnet publish backend/TaskManager.Api/TaskManager.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /src/frontend/dist ./wwwroot

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "TaskManager.Api.dll"]
