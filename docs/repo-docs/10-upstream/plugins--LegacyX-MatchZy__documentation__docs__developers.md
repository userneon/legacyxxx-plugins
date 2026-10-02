> **Хуулбар.** Эх сурвалж: [legacyxxx-plugins/LegacyX-MatchZy/documentation/docs/developers.md](https://github.com/userneon/legacyxxx-plugins/blob/main/LegacyX-MatchZy/documentation/docs/developers.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

Since this plugin is built on C#, [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) will be required if you intend to make changes in this plugin. Once you have that installed,

1. Clone the repository
2. Use `dotnet restore` to restore and install the dependencies.
3. Make your changes
4. Use `dotnet publish` command and you'll get a folder called `bin` in your plugin directory.
5. Navigate to `bin/Release/net8.0/publish/`and copy all the content from there and paste it into `csgo/addons/counterstrikesharp/plugins/MatchZy` (CounterStrikeSharp.API.dll and CounterStrikeSharp.API.pdb can be skipped)
6. It's done! Now you can test your changes, and also contribute to the plugin if you want to :p 
