using System.IO;
using System.Reflection;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

[assembly: ModInfo("TokenGrabber", Version = "1.1.1", Side = "Server",
	Description = "Adds a server-side command to grab the current server token before 1.23 for claiming on the server browser.",
	Authors = new []{ "Rennorb" })]
[assembly: ModDependency("game", "1.21.0")]

namespace TokenGrabber;

public class TokeGrabberSystem : ModSystem
{
	public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

	public override void StartServerSide(ICoreServerAPI api)
	{
		api.ChatCommands
			.Create("grabtoken")
			.RequiresPrivilege(Privilege.controlserver)
			.WithDescription("Grab the current server token for claiming on the server browser.")
			.HandleWith((cmdArgs) => {
				var worldAccessor = api.World;
				var systemsField = worldAccessor.GetType().GetField("Systems", BindingFlags.Instance | BindingFlags.NonPublic);
				if(systemsField == null) return TextCommandResult.Error("Systems field not found.");

				var systems = (object[])systemsField.GetValue(worldAccessor);
				
				object hbSystem = null;
				foreach(var system in systems) {
					if(system.GetType().FullName == "Vintagestory.Server.ServerSystemHeartbeat") {
						hbSystem = system;
						break;
					}
				}
				if(hbSystem == null) return TextCommandResult.Error("HB System not found.");

				var tokenField = hbSystem.GetType().GetField("token", BindingFlags.Instance | BindingFlags.NonPublic);
				if(tokenField == null) return TextCommandResult.Error("Field not found.");

				var token = (string)tokenField.GetValue(hbSystem);
				if(string.IsNullOrEmpty(token)) return TextCommandResult.Error("Server is not currently registered to the masterserver.");

				string msg = "";
				if(!api.Server.Config.HostedMode) {
					var tmpFile = Path.GetTempFileName();

					var onTheServer = cmdArgs.Caller.FromChatGroupId != GlobalConstants.ConsoleGroup ? " (on the server)" : "";
					try {
						var friendlyPath = Path.ChangeExtension(tmpFile, "txt");
						File.Move(tmpFile, friendlyPath);
						tmpFile = friendlyPath;
						msg = $"This token has also been written to '{tmpFile}'{onTheServer} so you don't have to manually type it out in case you cannot copy from this terminal.";
					}
					catch {
						msg = $"This token has also been written to '{tmpFile}'{onTheServer} so you don't have to manually type it out in case you cannot copy from this terminal. We tried creating a txt file but that failed, so just make sure to open this one in a normal text editor even though it has a wired extension (or none at all).";
					}
					File.WriteAllText(tmpFile, token);
				}

				if(cmdArgs.Caller.FromChatGroupId != GlobalConstants.ConsoleGroup) {
					msg += " Since you didn't run this for the server console, check your client-chat.log file, that will now also contain the token.";
				}

				return TextCommandResult.Success($"Current token: '{token}'. "+msg);
			});
	}
}
