CSPROJ="osu.Game.Rulesets.LazerTourney/osu.Game.Rulesets.LazerTourney.csproj"
SLN="osu.Game.Rulesets.LazerTourney.sln"

dotnet sln SLN add ../osu/osu.Game/osu.Game.csproj
dotnet add GAME_CSPROJ reference ../osu/osu.Game/osu.Game.csproj

dotnet sln SLN add ../osu/osu.Game.Tournament/osu.Game.Tournament.csproj
dotnet add GAME_CSPROJ reference ../osu/osu.Game.Tournament/osu.Game.Tournament.csproj
