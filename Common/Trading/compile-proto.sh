#!/bin/bash

# Packets
echo "Compiling packets"
protoc --proto_path=./ --csharp_out=./Packets/compiled/ ./Packets/*.proto

# Stores
echo "Compiling stores"
protoc --proto_path=./ --csharp_out=./Stores/compiled/ ./Stores/*.proto

echo "Done"