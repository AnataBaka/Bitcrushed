## BITCRUSHED
A simple rogue-like RPG in the style of an early 90's arcade game. Crawl your way throughout the bits or be crushed!

## Demo cadence
This branch is the shortened demo loop:
- **Biomes change every 3 floors** (Plains → Volcano → Swamp → Snowy Tundra, then repeat)
- **A boss appears every 2 floors** (stages 2, 4, 6, …)
- **EXP gain is 5×** (packs and bosses)

## TO PLAY: 
### Setup the Server (SpacetimeDB)
Download SpacetimeDB. Go to terminal and enter: 
```
spacetime start
```
Then enter:
```
"$env:USERPROFILE\AppData\Local\SpacetimeDB\spacetime.exe" generate --lang csharp --out-dir Assets/module_bindings --module-path spacetimedb -y
```
 Then enter the following with `XXXXXXXX` being the name of the server that will correspond to the variable `string database name` in `GameManger.cs` and `BattleBoostrap.cs`:
```
 & "$env:USERPROFILE\AppData\Local\SpacetimeDB\spacetime.exe" publish XXXXXXXX --server maincloud --yes --module-path spacetimedb --delete-data
```
Download `BITCRUSHED.zip` from either the realeases page or from the releases folder in the repository. Unzip the file and run the `Hophacks Project.exe`
## Inspiration
Our main inspiration were classic JRPGS and RPGS; most RPGS focus on 4 members which you solely control. However, what happened if you didn't control rather your friends got to join in on the fun as well! That is the inspiration behind our game Bitcrushed and why we chose to make a multiplayer RPG.
## What it does
Our game uses SpaceTimeDB to create a server where multiple people can use unity to play a unique co-op rogue-like. All you need to do is to launch the client for the game and you will be seamlessly connected with other players. You can play from up to 1-3 players and have infinite floors to explore! With infinite levels and a robust list of skills, there's plenty of depth for you to sink your teeth into.!
## How we built it
We built the project using C# and used Unity to interface with SpaceTimeDB to provide our multiplayer experience.
## Challenges we ran into
None of us have ever had previous experience with SpaceTimeDB and struggled with understanding the concept of using tables to store player data and values. This was in stark contrast to previous Unity projects where we stored almost all of our data within said engine. This struggle was it worth it though as after a lot of effort and hard work all three of us can say we have learned an invaluable skill, and have grown far more comfortable with C#, Unity, and SpaceTimeDB.

## Accomplishments that we're proud of
Real time-multiplayer, synced up player stats, consistent animations across different clients, and sprites for both enemies and the player.

## What we learned
Over the course of the past 36 hours we grew accustomed to setting up servers using SpaceTimeDB, using Unity as a front end, basics in implementing sprites and animations, and how to sync clients in C# for multiplayer.

## What's next for Bitcrushed
Our goal is to continuously add more biomes, more bosses, and more characters. We never want the players of this game to feel like the game is getting stale and monotonous, and by adding regular content we can keep our players happy!
