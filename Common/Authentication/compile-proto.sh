#!/bin/bash

# Credentials
echo "Compiling credentials"
protoc --proto_path=./ --csharp_out=./Credentials/compiled/ ./Credentials/*.proto

# Packets
echo "Compiling packets"
protoc --proto_path=./ --csharp_out=./Packets/compiled/ ./Packets/*.proto

echo "Done"