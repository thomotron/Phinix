# Build on top of Mono
FROM mono:latest

# Set our working directory to build from source
WORKDIR /src/

# Copy all project directories and files
COPY nuget.config ./
COPY Phinix.sln ./
COPY Server ./Server/
COPY Common ./Common/

# Restore NuGet packages and build
RUN nuget restore Phinix.sln && \
    msbuild Phinix.sln /t:Build /p:Configuration=TravisCI

# Move out of the src directory and make a spot for the server to sit
WORKDIR /server/

# Copy the build result into the server dir and clean up
RUN cp /src/Server/bin/Debug/*.dll ./ && \
    cp /src/Server/bin/Debug/PhinixServer.exe ./ && \
    rm -rf /src/

# Expose the default port
EXPOSE 16200

# Run the server
CMD ["mono", "PhinixServer.exe"]
