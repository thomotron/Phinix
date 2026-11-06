#!/bin/bash

# Packets
# Agora's packets use Trading's ProtoThing, so protoc also looks in the Trading project for imports
echo "Compiling packets"
protoc --proto_path=./ --proto_path=../Trading --csharp_out=./Packets/compiled/ ./Packets/*.proto

# Stores (what the server saves to disk)
echo "Compiling stores"
protoc --proto_path=./ --proto_path=../Trading --csharp_out=./Stores/compiled/ ./Stores/*.proto

echo "Done"
