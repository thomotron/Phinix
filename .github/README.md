<h1 align="center">PHINIX</h1>
<h4 align="center"><i>A RimWorld chat and trading mod</i></h4>
<p align="center"><img src="https://i.imgur.com/uXnIeua.png"></img></p>
<br><br>
<p align="center">
  <a href="https://github.com/PhinixTeam/Phinix/issues">
    <img src="https://img.shields.io/github/issues/PhinixTeam/Phinix.svg?style=flat-square" alt="Issues">
  </a>
  <a href="https://travis-ci.org/PhinixTeam/Phinix">
    <img src="https://img.shields.io/travis/PhinixTeam/Phinix.svg?style=flat-square" alt="Travis CI">
  </a>
  <a href="https://hub.docker.com/r/phinixteam/phinix">
    <img alt="Docker Build Status" src="https://img.shields.io/docker/cloud/build/phinixteam/phinix.svg?label=docker&style=flat-square">
  </a>
  <a href="https://discord.gg/d4Y5xks">
    <img src="https://img.shields.io/discord/363547745564360704.svg?colorB=7289DA&label=Discord&style=flat-square" alt="Discord">
  </a>
</p>
<br><br>

<!-- It'd be great if markdown had some way of aligning things, but ya gotta do what ya gotta do -->

# About
Phinix is a total rewrite of [Longwelwind's Phi mod](https://github.com/longwelwind/phi) that allows players to chat and trade items with each other within the game.  
It boasts improvements such as:
 - Chat message timestamps
 - Unread message alerts
 - Two-way trading GUI
 - Asynchronous trades (no need to respond straight away!)
 - Toggle-able name and chat formatting
 - ...and a bunch of other stuff that I'm sure I've forgotten to mention here

# Installation
## Client
1. Download `PhinixClient.zip` from the [releases page](https://github.com/PhinixTeam/Phinix/releases/latest)
2. Extract it into your `RimWorld/Mods` folder
(If you bought the game through Steam, this should be under somewhere like `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\`)
3. Download and install [HugsLib](https://github.com/UnlimitedHugs/RimworldHugsLib/releases/latest)
4. Load **HugsLib first**, *then* Phinix
5. Restart your game and you should be good to go

## Server
See the [wiki page](https://github.com/PhinixTeam/Phinix/wiki/Hosting-a-server) for server installation.

### Docker
There are automated server builds available through [Docker Hub](https://hub.docker.com/r/thomotron/phinix).  
See the [wiki page](https://github.com/PhinixTeam/Phinix/wiki/Hosting-a-server#docker-container) for more details.

# Usage
## Client
1. Load up a save or create a new one
2. Open the `Chat` tab toward the bottom-right
3. Click on `Settings` and enter the address and port of the server you want to connect to
4. Click `Connect`   
The chat panel should change from being greyed-out to a blue background. The user list on the right of the panel will update to show you and anyone else that is online.

## Server
1. Open `server.conf` with a text editor and change the settings as you see fit
2. Run `PhiServer.exe`
3. *(Optional)* Enter `help` to see all available commands

# FAQ
See the [wiki page](https://github.com/PhinixTeam/Phinix/wiki/FAQ) for a list of frequently-asked questions and answers.

# Developers
## Setting up your environment
### Game DLLs
The project expects `Assembly-CSharp.dll` and `UnityEngine.dll` to be present in `GameDlls/` just so we don't redistribute RimWorld's assemblies.
They can both be found in `<Path-to-RimWorld>/RimWorldWin64_Data/Managed/`. Either copy them in or make a symbolic link to them.

### Protobuf for Packet Compilation
Network data packets are defined in Protobuf, a structured, language-neutral de/serialisation framework designed by Google. If you want to make any changes to these packets, you will need a copy of [protoc](https://github.com/protocolbuffers/protobuf/releases/tag/v3.1.0) with C# support. Phinix uses Protobuf v3.1.0 due to limitations imposed by both RimWorld and Google.
