FROM mcr.microsoft.com/dotnet/aspnet:8.0.14-alpine3.21-arm64v8

ARG source
WORKDIR /app
EXPOSE 80
COPY ${source:-obj/Docker/publishARM} .

RUN apk update && \
    apk upgrade && \
    apk add netcat; \
    apk add icu-libs; \
    apk add icu-data-full; \
    chmod +x ./containerBootstrap.sh

# Disable the invariant mode (set in base image)
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ./containerBootstrap.sh