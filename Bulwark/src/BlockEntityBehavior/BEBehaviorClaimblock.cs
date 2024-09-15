using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;


namespace RoCBulwark {
    public class BlockEntityBehaviorClaimblock : BlockEntityBehavior {

        //=======================
        // D E F I N I T I O N S
        //=======================

            public Stronghold Stronghold { get; protected set; } = new Stronghold();
            
            //public ItemStack Banner      { get; protected set; } Skipping banners for a more "gamey" system
            public float CapturedPercent { get; protected set; }
            
            private int captureRadius;
            private float trueCaptureRadius;

            protected float TargetPercent => this.captureDirection switch {
                EnumCaptureDirection.Claim   => 1f,
                EnumCaptureDirection.Unclaim => 0f,
                _ => this.Stronghold.IsClaimed
                    ? this.Api.World.BlockAccessor.GetLightLevel(this.Pos, EnumLightLevelType.OnlySunLight) >= 16
                        ? this.NowClaimedUntilDay - this.Api.World.Calendar.TotalDays > 0.1
                            ? 1f
                            : 0f
                        : 0f
                    : 0f,
            }; // ..

            public HashSet<BlockEntityBehaviorLogistic> LogisticPoints { get; internal set; } = new ();

            protected float   cellarExpectancy;
            protected double? NowClaimedUntilDay => this.Api.World.Calendar.TotalDays
                + this.cellarExpectancy
                + this.LogisticPoints.Sum(logisticPoint => logisticPoint.CellarExpectancy)
                - (double)MathF.Pow(this.Stronghold.SiegeIntensity * 0.25f, 2);


            protected IPlayer              capturedBy;
            protected int                   capturedByGroup;
            protected EnumCaptureDirection captureDirection;
            protected float                captureDuration;

            //private FlagRenderer renderer;

            public BlockBehaviorClaimblock BlockBehavior { get; protected set; }

            private long? updateRef;
            private long? captureRef;
            private long? computeRef;
            private long? cellarRef;
            

        //===============================
        // I N I T I A L I Z A T I O N S
        //===============================

            public BlockEntityBehaviorClaimblock(BlockEntity blockEntity) : base(blockEntity) {}

            public override void Initialize(ICoreAPI api, JsonObject properties) {

                base.Initialize(api, properties);

                this.BlockBehavior = this.Block.GetBehavior<BlockBehaviorClaimblock>();

                int protectionRadius   = properties["protectionRadius"].AsInt(16);
                captureRadius = properties["captureRadius"].AsInt(4);
                this.captureDuration   = properties["captureDuration"].AsFloat(4f);
                this.Stronghold.Center = this.Pos;

                int worldheight = api.World.BlockAccessor.MapSizeY;
                this.Stronghold.Area   = new Cuboidi( // Defines Stronghold area, protection radius defined in block json.
                    this.Pos.AsVec3i - new Vec3i(protectionRadius, this.Pos.Y, protectionRadius), //Defines negative corner coordinate from center
                    this.Pos.AsVec3i + new Vec3i(protectionRadius, worldheight, protectionRadius) // Defines positive corner coordinate from center
                );

            this.Stronghold.CaptureArea = new Cuboidi( // Declare capture area. "half cube" area. Area that must be reached as a form of control point.
                this.Pos.AsVec3i - new Vec3i(captureRadius, 1, captureRadius),
                this.Pos.AsVec3i + new Vec3i(captureRadius, captureRadius, captureRadius)
                );
                trueCaptureRadius = (float)((captureRadius*Math.Sqrt(2))/2);

            // ..

            /*this.Banner?.ResolveBlockOrItem(this.Api.World);
            if (this.Banner != null && this.Api is ICoreClientAPI client) {

                client.Tesselator.TesselateShape(this.Banner.Item, Shape.TryGet(client, "RoCBulwark:shapes/flag/banner.json"), out MeshData meshData);
                this.renderer = new FlagRenderer(client, meshData, this.Pos, this, this.BlockBehavior.PoleTop, this.BlockBehavior.PoleBottom);
                client.Event.RegisterRenderer(this.renderer, EnumRenderStage.Opaque, "flag");

            } // if ..
            No banners
             */


                this.updateRef  = api.Event.RegisterGameTickListener(this.Update, 50);
                this.computeRef = api.Event.RegisterGameTickListener(this.ComputeCellar, 1000);
                this.cellarRef  = api.Event.RegisterGameTickListener(this.UpdateCellar, 6000);

                if (this.Stronghold .IsClaimed)
                    this.Api.ModLoader.GetModSystem<FortificationModSystem>().TryRegisterStronghold(this.Stronghold);

            } // void ..


            public override void OnBlockBroken(IPlayer byPlayer = null) {
                base.OnBlockBroken(byPlayer);
                //this.renderer?.Dispose();
                if (this.updateRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.updateRef.Value);
                if (this.computeRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.computeRef.Value);
                if (this.captureRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.captureRef.Value);
                if (this.cellarRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.cellarRef.Value);
                this.Stronghold.Unclaim(EnumUnclaimCause.FlagBroken);
                this.Api.ModLoader.GetModSystem<FortificationModSystem>().RemoveStronghold(this.Stronghold);
                //if (this.Banner != null) this.Api.World.SpawnItemEntity(this.Banner, this.Pos.ToVec3d());
            } // void ..


            public override void OnBlockRemoved() {
                base.OnBlockRemoved();
                //this.renderer?.Dispose();
                if (this.updateRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.updateRef.Value);
                if (this.computeRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.computeRef.Value);
                if (this.captureRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.captureRef.Value);
                if (this.cellarRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.cellarRef.Value);
                this.Api.ModLoader.GetModSystem<FortificationModSystem>().RemoveStronghold(this.Stronghold);
            } // void ..


            public override void OnBlockUnloaded() {
                base.OnBlockUnloaded();
                //this.renderer?.Dispose();
                if (this.updateRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.updateRef.Value);
                if (this.computeRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.computeRef.Value);
                if (this.captureRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.captureRef.Value);
                if (this.cellarRef.HasValue)  this.Api.Event.UnregisterGameTickListener(this.cellarRef.Value);
                this.Api.ModLoader.GetModSystem<FortificationModSystem>().RemoveStronghold(this.Stronghold);
            } // void ..


        //===============================
        // I M P L E M E N T A T I O N S
        //===============================

            public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc) {
                base.GetBlockInfo(forPlayer, dsc);

                if (this.Stronghold.Name is string name)
                    dsc.AppendLine("<font color=\"#ccc\"><i>" + Lang.Get("Banner of {0}", name) + "</i></font>");

                if (this.NowClaimedUntilDay is double claimedUntilDay) {
                    double remaining = claimedUntilDay - this.Api.World.Calendar.TotalDays;
                    
                    if (double.IsPositive(remaining)) {
                        if (this.Stronghold.PlayerName is not null)
                            if (this.Stronghold.GroupName is not null) {
                                dsc.AppendLine(Lang.Get(
                                    "Under {0}'s command in the name of {1} for {2:0.#} days",
                                    this.Stronghold.PlayerName,
                                    this.Stronghold.GroupName,
                                    remaining
                                )); // ..
                            } else dsc.AppendLine(Lang.Get(
                                    "Under {0}'s command for {1:0.#} days",
                                    this.Stronghold.PlayerName,
                                    remaining
                                )); // ..
                    } // if ..
                } // if ..
            } // void ..


            private void ComputeCellar(float _) {
                if (this.Stronghold.IsClaimed) {

                    List<BlockEntityContainer> cellars = new(4);
                    if (this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 1, 0,  0, this.Pos.dimension)) is BlockEntityContainer cellarA) cellars.Add(cellarA);
                    if (this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos(-1, 0,  0, this.Pos.dimension)) is BlockEntityContainer cellarB) cellars.Add(cellarB);
                    if (this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 0, 0,  1, this.Pos.dimension)) is BlockEntityContainer cellarC) cellars.Add(cellarC);
                    if (this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 0, 0, -1, this.Pos.dimension)) is BlockEntityContainer cellarD) cellars.Add(cellarD);

                   this.cellarExpectancy = 0f;

                    foreach(BlockEntityContainer cellar in cellars)
                        if (cellar != null)
                            foreach (ItemSlot itemSlot in cellar.Inventory)
                                if (itemSlot.Itemstack is ItemStack itemStack)
                                    if (itemStack.Collectible?.NutritionProps is FoodNutritionProperties foodNutrition)
                                        this.cellarExpectancy += foodNutrition.Satiety
                                            * itemStack.StackSize
                                            * (RoCBulwarkModSystem.ClaimDurationPerSatiety * (1f + this.BlockBehavior.ExpectancyBonus));

                } // if ..
            } // void ..


            private void UpdateCellar(float deltaTime) {
                if (this.Stronghold.IsClaimed) {

                    float nowDurationPerSatiety = RoCBulwarkModSystem.ClaimDurationPerSatiety * (1f + this.BlockBehavior.ExpectancyBonus);

                    float satiety       = 0f;
                    float targetSatiety = deltaTime / 86400f / this.Api.World.Calendar.SpeedOfTime / nowDurationPerSatiety;

                    BlockEntityContainer[] cellars = new BlockEntityContainer [4];
                    cellars[0] = this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 1, 0,  0, this.Pos.dimension));
                    cellars[1] = this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos(-1, 0,  0, this.Pos.dimension));
                    cellars[2] = this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 0, 0,  1, this.Pos.dimension));
                    cellars[3] = this.Api.World.BlockAccessor.GetBlockEntity<BlockEntityContainer>(this.Pos + new BlockPos( 0, 0, -1, this.Pos.dimension));

                    foreach(BlockEntityContainer cellar in cellars)
                        if (cellar != null)
                            foreach (ItemSlot itemSlot in cellar.Inventory)
                                if (satiety >= targetSatiety) return;
                                else if (itemSlot.Itemstack is ItemStack itemStack)
                                    if (itemStack.Collectible?.NutritionProps is FoodNutritionProperties foodNutrition) {

                                        int targetSize = GameMath.Min(
                                            itemStack.StackSize,
                                            GameMath.RoundRandom(this.Api.World.Rand, targetSatiety / (foodNutrition.Satiety * nowDurationPerSatiety)
                                            - satiety / (foodNutrition.Satiety * nowDurationPerSatiety))
                                        ); // ..

                                        satiety += foodNutrition.Satiety * targetSize * nowDurationPerSatiety;
                                        itemSlot.TakeOut(targetSize);
                                        itemSlot.MarkDirty();

                                    } // if ..
                } // if ..
            } // void ..


            private void Update(float deltaTime) {
            if (Api.Side == EnumAppSide.Server)
            {
                //Api.Logger.Debug(" \"void Update\" function call");

                this.CapturedPercent += GameMath.Clamp(this.TargetPercent - this.CapturedPercent, -deltaTime / this.captureDuration, deltaTime / this.captureDuration);
                if (this.CapturedPercent == 0f && this.Stronghold.IsClaimed)
                {
                    //Api.Logger.Debug(" \"void Update\" function condition pass, unclaim");

                    EnumUnclaimCause unclaimCause = this.cellarExpectancy == 0f
                            ? EnumUnclaimCause.EmptyCellar
                            : EnumUnclaimCause.Player;

                    /*if (this.captureDirection == EnumCaptureDirection.Unclaim) {
                        //this.Api.World.SpawnItemEntity(this.Banner, this.Pos.ToVec3d());
                        //this.Banner = null;
                        //this.renderer?.Dispose();
                        //this.renderer = null;
                    } // if .. */

                    this.Blockentity.MarkDirty();
                    this.Stronghold.Unclaim(unclaimCause);

                } // if ..
            }
            } // void ..


        public void TryStartCapture(IPlayer byPlayer) {

            if (byPlayer == null) return; //If there's no player, do nothing

            if (!(this.Api.World.BlockAccessor.GetTerrainMapheightAt(this.Pos) - this.Pos.Y <= RoCBulwarkModSystem.UndergroundClaimLimit)) // If the flag is underground, do nothing.
            {
                IServerPlayer serverPlayer = byPlayer as IServerPlayer;
                if (serverPlayer != null) serverPlayer.SendIngameError("stronghold-undergroundflag");
                return;
            }

            if (this.Stronghold.PlayerUID == byPlayer.PlayerUID) return; //If the player who owns it is trying to capture, do nothing.

            if (this.Stronghold.GroupUID.HasValue) //If the stronghold has a group 0->
            {
                if (byPlayer.GetGroup(this.Stronghold.GroupUID.Value) != null) return; // 0-> And if the player has the same group as the stronghold group, do nothing.
                // *IMPLEMENT* Stronghold transfering via command.
            };

            Api.Logger.Debug(" \"tryStartCapture\" {0} at {1}, current owner: {2}, ref {3}", byPlayer.PlayerName, byPlayer.Entity.Pos.XYZInt, this.capturedBy?.PlayerName, captureRef != null); //Debug print
                
            if (this.captureRef == null && this.capturedBy == null) { 
                
            // If not currently being captured, start capturing
            // Api.Logger.Debug("FIRST CONDITION: {0} at {1}", byPlayer.PlayerName, byPlayer.Entity.Pos.XYZInt); //Debug print


                this.Stronghold.contested = true;
                this.capturedBy = byPlayer;
                if(Api.Side == EnumAppSide.Server) this.capturedByGroup = this.Stronghold.AttemptTakeover(this.capturedBy);
                this.captureRef = this.Api.Event.RegisterGameTickListener(this.CaptureUpdate, 200);
                this.Blockentity.MarkDirty();


                

        } // if ..
    } // void ..


        private void CaptureUpdate(float deltaTime) {
            if (Api.Side == EnumAppSide.Server) { // Serverside updates only.
                if (this.capturedByGroup == -1)
                {

                }
                if (this.capturedBy != null) {
                    IPlayer[] playersAround = Api.World.GetPlayersAround(this.Pos.ToVec3d(), trueCaptureRadius, trueCaptureRadius);
                    List<IPlayer> attackers = new List<IPlayer>();
                    List<IPlayer> defenders = new List<IPlayer>();

                    for (int i = 0; i < playersAround.Length; i++)
                    {
                        if (this.Stronghold.CaptureArea.Contains(playersAround[i].Entity.Pos.XYZ))
                        {

                            if (this.Stronghold.IsClaimed)
                            {
                                if (playersAround[i].PlayerUID == this.Stronghold.PlayerUID || playersAround[i].GetGroup(this.Stronghold.GroupUID ?? -1) != null)
                                {
                                    Api.Logger.Debug("C, DEF: {0}", playersAround[i].PlayerName);
                                    defenders.Add(playersAround[i]);
                                }
                                else if (playersAround[i] == this.capturedBy || (playersAround[i].GetGroup(this.capturedByGroup) != null))
                                {
                                    Api.Logger.Debug("C, ATK: {0}", playersAround[i].PlayerName);
                                    attackers.Add(playersAround[i]);
                                }
                            } // Claimed Capture
                            else
                            {
                                if (playersAround[i] == this.capturedBy || (playersAround[i].GetGroup(this.capturedByGroup) != null))
                                {
                                    Api.Logger.Debug("U DEF: {0}", playersAround[i].PlayerName);
                                    defenders.Add(playersAround[i]);
                                } else
                                {
                                    Api.Logger.Debug("U ATK: {0}", playersAround[i].PlayerName);
                                    attackers.Add(playersAround[i]);
                                }
                            } // Unclaimed Capture

                        } // If player in capture zone
                    } // For loop over players in radius

                    // Capture direction logic. If attackers are greater than defenders, unclaim point,
                    this.captureDirection = attackers.Count > defenders.Count ? EnumCaptureDirection.Unclaim : (defenders.Count == attackers.Count ? EnumCaptureDirection.Still : EnumCaptureDirection.Claim);
                    Api.Logger.Debug("Def: {0}, Atk: {1}, Dir: {2}, Perc: {3}, capGrp-uid: {4}", defenders.Count, attackers.Count, this.captureDirection, this.CapturedPercent, this.capturedByGroup);


                    if (this.CapturedPercent == 1f)
                    {
                        if (!this.Stronghold.IsClaimed)

                            if (this.Api.ModLoader.GetModSystem<FortificationModSystem>().TryRegisterStronghold(this.Stronghold))
                            {

                                this.cellarExpectancy = GameMath.Max(this.cellarExpectancy, 0.2f);
                                this.Stronghold.Claim(capturedBy);
                                if (this.capturedByGroup != -1) this.Stronghold.ClaimGroup(capturedByGroup);
                                this.Stronghold.contested = false;
                                this.EndCapture();
                                this.Blockentity.MarkDirty();

                            }
                            else if (this.capturedBy is IServerPlayer serverPlayer)
                            {
                                this.Stronghold.contested = false;
                                this.EndCapture();
                                this.Blockentity.MarkDirty();
                                //serverPlayer.SendIngameError("stronghold-alreadyclaimed");
                            }
                    }
                }
                else
                {

                }
            }
        }

        public void EndCapture() {
            if (Api.Side == EnumAppSide.Server)
            {
                if (this.captureRef.HasValue) this.Api.Event.UnregisterGameTickListener(this.captureRef.Value);
                this.captureDirection = EnumCaptureDirection.Still;
                this.captureRef = null;
                this.capturedBy = null;
                this.capturedByGroup = -1;
                this.Blockentity.MarkDirty();
            }
        } // void ..


            //-------------------------------
            // T R E E   A T T R I B U T E S
            //-------------------------------

        public override void FromTreeAttributes(
            ITreeAttribute tree,
            IWorldAccessor worldForResolving
        ) {

            this.cellarExpectancy = tree.GetFloat("cellarExpectancy");
            this.CapturedPercent  = tree.GetFloat("capturedPercent");
            //this.Banner           = tree.GetItemstack("banner");
            this.captureDirection = (EnumCaptureDirection)tree.GetInt("captureDirection", (int)EnumCaptureDirection.Still);

            if (tree.GetString("claimedPlayerUID") is string playerUID
                && tree.GetString("claimedPlayerName") is string playerName
            ) {
                this.Stronghold.PlayerUID  = playerUID;
                this.Stronghold.PlayerName = playerName;
            } else {
                this.Stronghold.PlayerUID  = null;
                this.Stronghold.PlayerName = null;
            } // if ..

            if (tree.GetInt("claimedGroupUID") is int groupUID && groupUID != 0
                && tree.GetString("claimedGroupName") is string groupName
            ) {
                this.Stronghold.GroupUID  = groupUID;
                this.Stronghold.GroupName = groupName;
            } else {
                this.Stronghold.GroupUID  = null;
                this.Stronghold.GroupName = null;
            } // if ..

            if (tree.GetString("areaName") is string name) this.Stronghold.Name = name;

            this.Stronghold.SiegeIntensity = tree.GetFloat("siegeIntensity");

            base.FromTreeAttributes(tree, worldForResolving);

        } // void ..


                public override void ToTreeAttributes(ITreeAttribute tree) {

                    if (this.Block != null)                              tree.SetString("forBlockCode", this.Block.Code.ToShortString());
                    if (this.Stronghold.Name is string name)             tree.SetString("areaName", name);
                    if (this.Stronghold.PlayerUID is string playerUID)   tree.SetString("claimedPlayerUID", playerUID);   else tree.RemoveAttribute("claimedPlayerUID");
                    if (this.Stronghold.PlayerName is string playerName) tree.SetString("claimedPlayerName", playerName); else tree.RemoveAttribute("claimedPlayerName");
                    if (this.Stronghold.GroupUID is int groupUID)        tree.SetInt("claimedGroupUID", groupUID);        else tree.RemoveAttribute("claimedGroupUID");
                    if (this.Stronghold.GroupName is string groupName)   tree.SetString("claimedGroupName", groupName);   else tree.RemoveAttribute("claimedGroupName");

                    //if (this.Banner != null) tree.SetItemstack("banner", this.Banner); else tree.RemoveAttribute("banner");

                    tree.SetFloat("siegeIntensity",   this.Stronghold.SiegeIntensity);
                    tree.SetFloat("cellarExpectancy", this.cellarExpectancy);
                    tree.SetFloat("capturedPercent",  this.CapturedPercent);
                    tree.SetInt("captureDirection", (int)this.captureDirection);

                    base.ToTreeAttributes(tree);

                } // void ..
    } // class ..
} // namespace ..
