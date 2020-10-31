#!/bin/sh -e
echo Rebuilding Ixian Lite Wallet...
echo Cleaning previous build
msbuild IxianLiteWallet.sln /p:Configuration=Release /target:Clean
echo Removing packages
rm -rf packages
echo Restoring packages
nuget restore IxianLiteWallet.sln
echo Building Ixian Lite Wallet
msbuild IxianLiteWallet.sln /p:Configuration=Release
echo Done rebuilding Ixian Lite Wallet