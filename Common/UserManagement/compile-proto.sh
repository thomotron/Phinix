#!/bin/bash

# Users
echo "Compiling users"
protoc --proto_path=./ --csharp_out=./Users/compiled/ ./Users/*.proto

echo "Done"