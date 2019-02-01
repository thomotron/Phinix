# Build on top of Mono
FROM mono:latest

# Set our working directory
WORKDIR /

# Copy all project directories and files
COPY Common /Common/
COPY Server /Server/
COPY nuget.config /
COPY Phinix.sln /

# Clean out the previous build and make sure we have a clean environment
RUN msbuild Phinix.sln /t:Clean /p:Configuration=TravisCI

# Resotre NuGet packages
RUN nuget restore Phinix.sln

# Build the server
RUN msbuild Phinix.sln /t:Build /p:Configuration=TravisCI

# Move our working directory to the build directory
WORKDIR /Server/bin/Debug/

# Expose the default port
EXPOSE 16200

# Run the server
CMD ["mono", "PhinixServer.exe"]
