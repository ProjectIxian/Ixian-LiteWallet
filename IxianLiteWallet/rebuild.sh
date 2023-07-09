#!/bin/sh -e
echo Rebuilding Ixian Lite Wallet...
echo Cleaning previous build
dotnet clean --configuration Release
echo Restoring packages
dotnet restore
echo Building Ixian Lite Wallet
dotnet build --configuration Release
echo Done rebuilding Ixian Lite Wallet