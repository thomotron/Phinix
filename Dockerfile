# Build on top of Mono
FROM mono:latest AS build

# Set our working directory to build from source
WORKDIR /src/

# Copy all project directories and files
COPY nuget.config ./
COPY Phinix.sln ./
COPY Server ./Server/
COPY Common ./Common/
COPY Dependencies ./Dependencies/

# Restore NuGet packages and build
RUN nuget restore Phinix.sln && \
    msbuild Phinix.sln /t:Build /p:Configuration=TravisCI

# Start fresh using a lighter Alpine image
FROM frolvlad/alpine-mono:latest

# Make a spot for the server to sit
WORKDIR /server/

# Copy the build result into the server dir
COPY --from=build /src/Server/bin/Debug/*.dll /src/Server/bin/Debug/PhinixServer.exe ./

# Expose the default port
EXPOSE 16200

# Run the server
CMD ["mono", "PhinixServer.exe"]
