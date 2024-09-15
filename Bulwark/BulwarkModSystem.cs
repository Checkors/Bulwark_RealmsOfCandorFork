using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;


namespace RoCBulwark {
    public class RoCBulwarkModSystem : ModSystem {

        public static float ClaimDurationPerSatiety     { get; private set; }
        public static int   UndergroundClaimLimit       { get; private set; }
        public static bool  AllStoneBlockRequirePickaxe { get; private set; }

        public override bool ShouldLoad(EnumAppSide forSide) => true;
        public override void Start(ICoreAPI api) {

            base.Start(api);
            api.RegisterBlockClass("BlockBarricade", typeof(BlockBarricade));
            api.RegisterBlockBehaviorClass("Flag", typeof(BlockBehaviorFlag));
            api.RegisterBlockBehaviorClass("Logistic", typeof(BlockBehaviorLogistic));

            api.RegisterBlockEntityBehaviorClass("FlagEntity", typeof(BlockEntityBehaviorFlag));
            api.RegisterBlockEntityBehaviorClass("LogisticEntity", typeof(BlockEntityBehaviorLogistic));

            // RoC Claim Blocks
            api.RegisterBlockBehaviorClass("Claimblock", typeof(BlockBehaviorClaimblock));
            api.RegisterBlockEntityBehaviorClass("ClaimblockEntity", typeof(BlockEntityBehaviorClaimblock));

            JsonObject modConfig = api.LoadModConfig("RoCBulwarkModConfig.json");
            RoCBulwarkModSystem.ClaimDurationPerSatiety     = modConfig?["claimDurationPerSatiety"]?.AsFloat(0.0025f) ?? 0.0025f;
            RoCBulwarkModSystem.UndergroundClaimLimit       = modConfig?["undergroundClaimLimit"]?.AsInt(8)           ?? 8;
            RoCBulwarkModSystem.AllStoneBlockRequirePickaxe = modConfig?["allStoneBlockRequirePickaxe"]?.AsBool(true) ?? true;

        } // void ..

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            api.Event.PlayerJoin += (serverPlayer) =>
            {
                string getCaptureGroup;
                if (serverPlayer.ServerData.CustomPlayerData.TryGetValue("CaptureGroup", out getCaptureGroup)) {
                int groupUID = int.Parse(getCaptureGroup);
                    if (serverPlayer.GetGroup(groupUID) == null && groupUID != -1)
                    {
                        serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.GetL(serverPlayer.LanguageCode, "capturegroup-notingroup", api.Groups.PlayerGroupsById[groupUID].Name), EnumChatType.Notification);
                        serverPlayer.ServerData.CustomPlayerData["CaptureGroup"] = "-1"; 
                    }
                } 
                else
                {
                    serverPlayer.ServerData.CustomPlayerData.TryAdd("CaptureGroup", "-1");
                }

            };
        }

        public override void AssetsFinalize(ICoreAPI api) {
            base.AssetsFinalize(api);
            foreach (Block block in api.World.Blocks) {
                if (RoCBulwarkModSystem.AllStoneBlockRequirePickaxe
                    && block.BlockMaterial      == EnumBlockMaterial.Stone
                    && block.Replaceable        <= 200
                    && block.CollisionBoxes     != null
                    && block.RequiredMiningTier <  2
                ) block.RequiredMiningTier = 2;
                
                if (block is BlockDoor || block.HasBehavior<BlockBehaviorDoor>()) {
                    if (block.BlockMaterial == EnumBlockMaterial.Metal && block.RequiredMiningTier < 3)
                        block.RequiredMiningTier = 3;
                    else if (block.BlockMaterial == EnumBlockMaterial.Wood && block.RequiredMiningTier < 2)
                        block.RequiredMiningTier = block.Code.EndVariant() == "crude" ? 1 : 2;
                } // if ..
            } // foreach ..
        } // void ..
    } // class ..
} // namespace ..
