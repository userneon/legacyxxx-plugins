# LEGACY-X Plugins

`legacyxxx-plugins` нь LEGACY-X community-ийн CS2 server-side plugin repository юм. Одоогийн production plugin нь CounterStrikeSharp дээр ажиллах LEGACY-X AdminPlus command bridge бөгөөд dashboard-аас ирсэн RCON command-уудыг CS2 player/server action болгон хэрэгжүүлнэ.

## Build requirements

CS2 server дээр Metamod:Source болон CounterStrikeSharp суусан байна. Build хийхэд .NET 8 SDK шаардлагатай.

```bash
cd adminplus/plugin/AdminPlus
dotnet build -c Release
```

Output:

```text
adminplus/plugin/AdminPlus/bin/Release/net8.0/AdminPlus.dll
```

## Deploy

Build болсон DLL-г CS2 host-ийн дараах folder руу байрлуулна.

```text
csgo/addons/counterstrikesharp/plugins/AdminPlus/AdminPlus.dll
```

Server restart хийх эсвэл RCON-оор:

```text
css_plugins unload AdminPlus
css_plugins load AdminPlus
```

Server log-д `[LEGACY-X AdminPlus] Loaded — production command bridge ready.` гэж харагдана.

## Current command contract

| Command family | Examples |
|---|---|
| Player info | `sm_playerinfo_all` |
| Player actions | `sm_respawn`, `sm_setteam`, `sm_sethp`, `sm_freeze`, `sm_unfreeze`, `sm_god`, `sm_slap` |
| Economy/weapons | `sm_givemoney`, `sm_givemoney_all`, `sm_giveweapon`, `sm_giveweapon_all` |
| Player cleanup | `sm_stripweapons`, `kickid` |

## Ownership boundary

RCON bridge, database audit logging болон Discord outbound webhook нь `legacyxxx-backend` repository-д байна. Энэ repository зөвхөн CS2 plugin source болон server deployment artifact-г эзэмшинэ. Frontend source энд байхгүй.

## References

Upstream implementation: [dede177/cs2-admin-plus](https://github.com/dede177/cs2-admin-plus). Framework: [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp).
