#!/bin/bash

# Users
echo "Compiling users"
protoc --proto_path=./ --csharp_out=./Users/compiled/ ./Users/*.proto

# Packets
echo "Compiling packets"
protoc --proto_path=./ --csharp_out=./Packets/compiled/ ./Packets/*.proto

echo "Done"