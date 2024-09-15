using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;


namespace RoCBulwark {
    public class BlockBehaviorClaimblock : BlockBehavior {

        //=======================
        // D E F I N I T I O N S
        //=======================

            //protected ItemStack[] bannerStacks;
            public float ExpectancyBonus { get; protected set; }


        //===============================
        // I N I T I A L I Z A T I O N S
        //===============================
        
            public BlockBehaviorClaimblock(Block block) : base(block) {}

            public override void Initialize(JsonObject properties) {
                base.Initialize(properties);
                this.ExpectancyBonus = properties["expectancyBonus"].AsFloat(0f);
            } // void ..


            public override void OnLoaded(
                ICoreAPI api) {
                base.OnLoaded(api);
            if (api.Side == EnumAppSide.Client) {
                
                ICoreClientAPI capi = api as ICoreClientAPI;
                
                    }; // ..
            } // void ..


        //===============================
        // I M P L E M E N T A T I O N S
        //===============================

            public override WorldInteraction[] GetPlacedBlockInteractionHelp(
                IWorldAccessor world,
                BlockSelection selection,
                IPlayer forPlayer,
                ref EnumHandling handling
            ) {
                return new WorldInteraction[2] {
                    new () {
                        ActionLangCode = "blockhelp-claim-capture",
                        MouseButton    = EnumMouseButton.Right,
                    }, // ..
                    new () {
                        ActionLangCode = "blockhelp-claim-unclaim",
                        MouseButton    = EnumMouseButton.Right,
                        HotKeyCode     = "ctrl",
                    }, // ..
                    /*new () {
                        ActionLangCode = "blockhelp-claim-set",
                        MouseButton    = EnumMouseButton.Right,
                        Itemstacks     = this.bannerStacks/
                    } // ..*/
                }; // ..
            } // ..


            public override bool OnBlockInteractStart(
                IWorldAccessor world,
                IPlayer byPlayer,
                BlockSelection blockSel,
                ref EnumHandling handling
            ) {

                handling = EnumHandling.PreventDefault;
                world.BlockAccessor
                    .GetBlockEntity(blockSel.Position)?
                    .GetBehavior<BlockEntityBehaviorClaimblock>()?
                    .TryStartCapture(byPlayer);

                return true;

            } // bool ..


            public override bool OnBlockInteractStep(
                float secondsUsed,
                IWorldAccessor world,
                IPlayer byPlayer,
                BlockSelection blockSel,
                ref EnumHandling handling
            ) {

                handling = EnumHandling.PreventSubsequent;
                return true;

            } // bool ..


            public override void OnBlockInteractStop(
                float secondsUsed,
                IWorldAccessor world, 
                IPlayer byPlayer,
                BlockSelection blockSel,
                ref EnumHandling handling
            ) {

                handling = EnumHandling.PreventDefault;
                world.BlockAccessor
                    .GetBlockEntity(blockSel.Position)?
                    .GetBehavior<BlockEntityBehaviorFlag>()?
                    .EndCapture();

            } // void ..


            public override bool OnBlockInteractCancel(
                float secondsUsed,
                IWorldAccessor world,
                IPlayer byPlayer,
                BlockSelection blockSel,
                ref EnumHandling handling
            ) {

                handling = EnumHandling.PreventDefault;
                world.BlockAccessor
                    .GetBlockEntity(blockSel.Position)?
                    .GetBehavior<BlockEntityBehaviorFlag>()?
                    .EndCapture();

                return true;

            } // bool ..
    } // class ..
} // namespace ..
