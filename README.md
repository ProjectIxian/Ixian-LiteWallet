# Ixian-LiteWallet
Simple command line interface (CLI) wallet for Ixian

## Development branches

There are two main development branches:
* master: This branch is used to build the binaries for the official IXIAN DLT network. It should change slowly and be quite well-tested. This is also the default branch for anyone who wishes to build their Ixian software from source.
* development: This is the main development branch and the source for testnet binaries. The branch might not always be kept bug-free, if an extensive new feature is being worked on. If you are simply looking to build a current testnet binary yourself, please use one of the release tags which will be associated with the master branch.


## Running
Download the latest binary release or you can compile the code yourself.
### Windows
Double-click on the IxianLiteWallet.exe to start the wallet.

### Linux
Download and install the latest Mono release for your Linux distribution. 
The default Mono versions shipped with most common distributions are outdated.

Go to the [Mono official website](https://www.mono-project.com/download/stable/#download-lin) and follow the steps for your Linux distribution.
We recommend you install the **mono-complete** package.

Open a terminal and navigate to the IxianLiteWallet folder, then type
```
mono IxianLiteWallet.exe
```
to start the wallet.

## Building
### Windows
Visual Studio 2017 is required (Community Edition is fine), you can get it from here: [Visual Studio](https://visualstudio.microsoft.com/)

Several NuGetPackages are downloaded automatically during the build process.

### Linux
Download and install the latest Mono release for your Linux distribution. The default Mono versions shipped with most common distributions are outdated.

Go to the [Mono official website](https://www.mono-project.com/download/stable/#download-lin) and follow the steps for your Linux distribution.

We recommend you install the **mono-complete** package.

For Debian based distributions such as Ubuntu, type
```
sudo apt install mono-complete nuget msbuild git gcc
```
or if you have a Redhat based distribution, type
```
sudo yum install mono-complete nuget msbuild git gcc
```

Next you'll need to build the IxianLiteWallet solution. You can do this by typing the following commands in the terminal:
```
git clone https://github.com/ProjectIxian/Ixian-Core.git
git clone https://github.com/ProjectIxian/Ixian-LiteWallet.git
cd Ixian-LiteWallet/IxianLiteWallet
nuget restore IxianLiteWallet.sln
msbuild IxianLiteWallet.sln /p:Configuration=Release
```
The IxianLiteWallet will be compiled and placed in the IxianLiteWallet/bin/Release/ folder.

## Get in touch / Contributing

If you feel like you can contribute to the project, or have questions or comments, you can get in touch with the team through Discord: (https://discord.gg/dbg9WtR)

## Pull requests

If you would like to send an improvement or bugfix to this repository, but without permanently joining the team, follow these approximate steps:

1. Fork this repository
2. Create a branch (preferably with a name that describes the change)
3. Create commits (the commit messages should contain some information on what and why was changed)
4. Create a pull request to this repository for review and inclusion.

