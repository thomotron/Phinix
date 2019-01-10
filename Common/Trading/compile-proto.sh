#!/bin/bash

# Packets
echo "Compiling packets"
protoc --proto_path=./ --csharp_out=./Packets/compiled/ ./Packets/*.proto

echo "Done"