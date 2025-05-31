using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

[assembly: ModInfo("StepUp", Version = "1.2.0", Side = "Client",
	Description = "Doubles players' step height to allow stepping up full blocks",
	Website = "https://www.vintagestory.at/forums/topic/3349-stepup-v120/",
	Authors = new []{ "copygirl" })]
[assembly: ModDependency("game")]

namespace StepUp
{
	public class StepUpSystem : ModSystem
	{
		public override void StartClientSide(ICoreClientAPI api)
		{
			api.Event.PlayerEntitySpawn += player =>
				player.Entity.GetBehavior<EntityBehaviorControlledPhysics>().stepHeight = 1.2F;
		}
	}
}
