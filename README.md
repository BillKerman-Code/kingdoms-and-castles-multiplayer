# Kingdoms and Castles Multiplayer

Steam multiplayer for [Kingdoms and Castles](https://store.steampowered.com/app/569480/). Host or
join through a Steam lobby; every player builds and runs their own kingdom on a shared map, with
troops, trade, diplomacy and saved sessions.

**Install:** subscribe on the Steam Workshop:
<https://steamcommunity.com/sharedfiles/filedetails/?id=3751021307>

This repository is the source of that Workshop item. It is still in beta.

## Reporting a bug

A report with logs gets fixed. A report without them usually cannot be. Please attach **both**:

- **Player.log**, which is where most errors actually end up:
  `%USERPROFILE%\AppData\LocalLow\LionShield\Kingdoms and Castles\Player.log`
  (paste that into the Windows Explorer address bar). It is overwritten every launch, so copy it
  **before** starting the game again.
- **output.txt**, the mod's own log: in Steam, right-click Kingdoms and Castles, *Manage >
  Browse local files*, go up to `steamapps\workshop\content\569480\3751021307\`. It grows across
  runs, so the end of the file is the part that matters.

Say how many players were in the session, who was hosting, and whether it was a new game or a
loaded save. Open an issue here, or comment on the Workshop page.

## Contributing

Fixes are very welcome, and they ship on the main Workshop item with your name on them. See
[CONTRIBUTING.md](CONTRIBUTING.md).

## Credits

- The original **KCM** multiplayer mod (Workshop 3105755541), which this continues with its
  author's permission.
- **Bill Kerman**, whose community patch contributed a large share of 0.14.0: guest save loading,
  treasuries, harvests, the save transfer, dragons, export prices and diplomacy popups.
- **[RiptideNetworking](https://github.com/RiptideNetworking/Riptide)** by Tom Weiland, MIT
  licensed, see [LICENSE.md](LICENSE.md).

Maintained by BrassyCrane.
