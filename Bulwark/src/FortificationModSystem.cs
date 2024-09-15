using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Client;


namespace RoCBulwark
{
    public class FortificationModSystem : ModSystem
    {

        //=======================
        // D E F I N I T I O N S
        //=======================

        protected HashSet<Stronghold> strongholds = new();
        protected ICoreAPI api;

        public delegate void NewStrongholdDelegate(Stronghold stronghold);
        public event NewStrongholdDelegate StrongholdAdded;


        //===============================
        // I N I T I A L I Z A T I O N S
        //===============================

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;
        } // void ..


        public override void StartServerSide(ICoreServerAPI api)
        {

            base.StartServerSide(api);
            api.Event.CanPlaceOrBreakBlock += this.BlockChangeAttempt;
            //api.Event.DidPlaceBlock += this.PlaceBlockEvent; // Deprecated and removed, caused block duplications.
            //api.Event.DidBreakBlock += this.BreakBlockEvent; // Deprecated, use case requires stopping block breaking and placement.
            api.Event.PlayerDeath += this.PlayerDeathEvent;


            // Baseline Bulwark command registrations
            api.ChatCommands
                .Create("stronghold")
                .RequiresPrivilege(Privilege.chat)
                .BeginSubCommand("name")
                    .WithDescription("Name the claimed area you are in")
                    .WithArgs(api.ChatCommands.Parsers.Word("name"))
                    .HandleWith(new OnCommandDelegate(Cmd_StrongholdName))
                .EndSubCommand()
                .BeginSubCommand("league")
                    .WithDescription("Affiliate the claimed area you are in with a group")
                    .WithArgs(api.ChatCommands.Parsers.Word("group name"))
                    .HandleWith(new OnCommandDelegate(Cmd_StrongholdLeague)) // ..
                .EndSubCommand()
                .BeginSubCommand("stopleague")
                    .WithDescription("Stops the affiliation with a group")
                    .WithArgs(api.ChatCommands.Parsers.Word("group name"))
                    .HandleWith(new OnCommandDelegate(Cmd_StrongholdUnleague))
                .EndSubCommand()
            // RoC Bulwark command registrations
                .BeginSubCommand("capturegroup")
                    .RequiresPlayer()
                    .RequiresPrivilege(Privilege.chat)
                    .BeginSubCommand("set")
                        .WithArgs(api.ChatCommands.Parsers.Word("groupname"))
                        .HandleWith(new OnCommandDelegate(Cmd_SetCaptureGroup))
                    .EndSubCommand()
                    .BeginSubCommand("show")
                        .IgnoreAdditionalArgs()
                        .HandleWith(new OnCommandDelegate(Cmd_GetCaptureGroup));
           
        } 

        //===============================
        // Bulwark Command Handlers
        //===============================

        private TextCommandResult Cmd_StrongholdName(TextCommandCallingArgs args)
        {

            string callerUID = args.Caller.Player.PlayerUID;
            if (this.strongholds?.FirstOrDefault(
                stronghold => stronghold.PlayerUID == callerUID
                && stronghold.Area.Contains(args.Caller.Player.Entity.ServerPos.AsBlockPos),
                null
            ) is Stronghold area)
            {

                area.Name = args[0].ToString();
                this.api.World.BlockAccessor.GetBlockEntity(area.Center).MarkDirty();

            }
            else TextCommandResult.Success(Lang.GetL(args.LanguageCode,"You're not in a stronghold you claimed"));
            return TextCommandResult.Success();

        }

        private TextCommandResult Cmd_StrongholdLeague(TextCommandCallingArgs args)
        {

            string callerUID = args.Caller.Player.PlayerUID;
            if (this.strongholds?.FirstOrDefault(
                stronghold => stronghold.PlayerUID == callerUID
                && stronghold.Area.Contains(args.Caller.Player.Entity.ServerPos.AsBlockPos),
                null
            ) is Stronghold area)
            {
                if ((this.api as ICoreServerAPI).Groups.GetPlayerGroupByName(args[0].ToString()) is PlayerGroup playerGroup)
                {

                    area.ClaimGroup(playerGroup);
                    this.api.World.BlockAccessor.GetBlockEntity(area.Center).MarkDirty();

                }
                else TextCommandResult.Success(Lang.GetL(args.LanguageCode,"No such group found"));
            }
            else TextCommandResult.Success(Lang.GetL(args.LanguageCode,"You're not in a stronghold you claimed"));
            return TextCommandResult.Success();

        }

        private TextCommandResult Cmd_StrongholdUnleague(TextCommandCallingArgs args)
        {

            string callerUID = args.Caller.Player.PlayerUID;
            if (this.strongholds?.FirstOrDefault(
                stronghold => stronghold.PlayerUID == callerUID
                && stronghold.Area.Contains(args.Caller.Player.Entity.ServerPos.AsBlockPos),
                null
            ) is Stronghold area)
            {
                if ((this.api as ICoreServerAPI).Groups.GetPlayerGroupByName(args[0].ToString()) is PlayerGroup playerGroup)
                {

                    area.UnclaimGroup();
                    this.api.World.BlockAccessor.GetBlockEntity(area.Center).MarkDirty();

                }
                else TextCommandResult.Success(Lang.GetL(args.LanguageCode,"No such group found"));
            }
            else TextCommandResult.Success(Lang.GetL(args.LanguageCode,"You're not in a stronghold you claimed"));
            return TextCommandResult.Success();

        }

        //===============================
        // RoC Command Handlers
        //===============================

        // Show Claim Zones, (WIP)
        private TextCommandResult Cmd_ShowClaimAreas(TextCommandCallingArgs args)
        {
            if (args.Caller == null)
            {
                return TextCommandResult.Error("No Player");
            }

            
            return TextCommandResult.Success();
            //return TextCommandResult.Error("Fucked");
        }

        // Set Capture Group. Used to set attacker group when capturing control point.
        private TextCommandResult Cmd_SetCaptureGroup(TextCommandCallingArgs args)
        {
            ICoreServerAPI sapi = api as ICoreServerAPI;
            string groupName = (string)args[0];

            sapi.Logger.Debug("ARGS: {0}, Type:{1}, gname: {2}", args[0], args[0].GetType(), groupName);

            if (groupName != null)
            {
                PlayerGroup mainGroup = sapi.Groups.GetPlayerGroupByName(groupName);

                if (mainGroup == null) return TextCommandResult.Error("RoCBulwark:capturegroup-nosuchgroup"); // No such group retrieved

                if (args.Caller.Player.GetGroup(mainGroup.Uid) != null) {

                    if (!sapi.PlayerData.PlayerDataByUid[args.Caller.Player.PlayerUID].CustomPlayerData.TryAdd("CaptureGroup", mainGroup.Uid.ToString()))
                    {
                        sapi.PlayerData.PlayerDataByUid[args.Caller.Player.PlayerUID].CustomPlayerData["CaptureGroup"] = mainGroup.Uid.ToString();
                    }
                    return TextCommandResult.Success(Lang.GetL(args.LanguageCode, "RoCBulwark:capturegroup-setsuccess", mainGroup.Name)); // Success state
                }
                else return TextCommandResult.Error(Lang.GetL(args.LanguageCode, "RoCBulwark:capturegroup-notingroup", groupName)); // No in requested group

            }
            else return TextCommandResult.Error("Null group name, debug error, contact dev"); //Args nulled out, somehow
            

        }

        // Set Capture Group. Used to set attacker group when capturing control point.
        private TextCommandResult Cmd_GetCaptureGroup(TextCommandCallingArgs args)
        {
            ICoreServerAPI sapi = api as ICoreServerAPI;
            string getGroupUID;
            if (sapi.PlayerData.PlayerDataByUid[args.Caller.Player.PlayerUID].CustomPlayerData.TryGetValue("CaptureGroup", out getGroupUID))
            {
                if (int.Parse(getGroupUID) > 0) return TextCommandResult.Success(Lang.GetL(args.LanguageCode, "RoCBulwark:capturegroup-current", sapi.Groups.PlayerGroupsById[int.Parse(getGroupUID)].Name));
                else return TextCommandResult.Error(Lang.GetL(args.LanguageCode, "RoCBulwark:capturegroup-nosetgroup")); //unset group.
            }
            else return TextCommandResult.Error(Lang.GetL(args.LanguageCode, "RoCBulwark:capturegroup-nosetgroup")); //Never set group


        }


        //===============================
        // Event Handlers
        //===============================
        private bool BlockChangeAttempt(IServerPlayer byPlayer, BlockSelection blockSel, out string claimant)
        {
            claimant = null;

            if (blockSel != null)
            {
                string blockname = api.World.BlockAccessor.GetBlock(blockSel.Position).GetPlacedBlockName(api.World, blockSel.Position);
                //api.Logger.Debug("[RoCBulwark_BCA] {0} attempted place/remove {1}, at position {2}", byPlayer.PlayerName.ToString(), blockname, blockSel.Position.ToString());
                if (!(this.HasPrivilege(byPlayer, blockSel, out _) || byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative))
                {
                    //api.Logger.Debug("No Access");
                    claimant = Lang.GetL(byPlayer.LanguageCode, "RoCBulwark:stronghold-noaccess");
                    return false;
                }
                else
                {
                    //api.Logger.Debug("Access");
                    claimant = null;
                    return true;
                }

            } else
            {
                claimant = "Null selection";
                return false;
            }
        }

        private void PlayerDeathEvent(
            IServerPlayer forPlayer,
            DamageSource damageSource
        )
        {
            if (this.strongholds.FirstOrDefault(
                    area => area.PlayerUID == forPlayer.PlayerUID
                    || (forPlayer.Groups?.Any(group => group.GroupUid == area.GroupUID) ?? false), null
                ) is Stronghold stronghold
            )
            {

                Entity byEntity = damageSource.CauseEntity ?? damageSource.SourceEntity;

                if (byEntity is EntityPlayer playerCause
                    && stronghold.Area.Contains(byEntity.ServerPos.AsBlockPos)
                    && !(playerCause.Player.Groups?.Any(group => group.GroupUid == stronghold.GroupUID) ?? false
                        || playerCause.PlayerUID == stronghold.PlayerUID)
                ) stronghold.IncreaseSiegeIntensity(1f, byEntity);

                else if (byEntity.WatchedAttributes.GetString("guardedPlayerUid") is string playerUid
                    && this.api.World.PlayerByUid(playerUid) is IPlayer byPlayer
                    && stronghold.Area.Contains(byEntity.ServerPos.AsBlockPos)
                    && !(byPlayer.Groups?.Any(group => group.GroupUid == stronghold.GroupUID) ?? false
                        || byPlayer.PlayerUID == stronghold.PlayerUID)
                    ) stronghold.IncreaseSiegeIntensity(1f, damageSource.CauseEntity);
            } // if ..
        } // void ..

        //===============================
        // Stronghold Handlers
        //===============================

        public bool TryRegisterStronghold(Stronghold stronghold)
        {

            stronghold.Api = this.api;
            if (this.strongholds.Contains(stronghold)) return true;
            else if (this.strongholds.Any(x => x.Area.Intersects(stronghold.Area))) return false;
            else this.strongholds.Add(stronghold);

            stronghold.UpdateRef = stronghold.Api
                .Event
                .RegisterGameTickListener(stronghold.Update, 2000, 1000);

            this.StrongholdAdded?.Invoke(stronghold);
            return true;
        } // void ..


        public void RemoveStronghold(Stronghold stronghold)
        {
            if (stronghold is not null)
            {
                if (stronghold.UpdateRef.HasValue) stronghold.Api.Event.UnregisterGameTickListener(stronghold.UpdateRef.Value);
                this.strongholds.Remove(stronghold);
            } // if ..
        } // void ..


        public bool TryGetStronghold(BlockPos pos, out Stronghold value)
        {
            if (this.strongholds?.FirstOrDefault(stronghold => stronghold.Area.Contains(pos), null) is Stronghold area)
            {
                value = area;
                return true;
            }
            else
            {
                value = null;
                return false;
            } // if ..
        } // bool ..


        public bool HasPrivilege(
            IPlayer byPlayer,
            BlockSelection blockSel,
            out Stronghold area
        )
        {

            area = null;

            if (this.strongholds == null) return true;
            if (this.strongholds?.Count == 0) return true;

            bool privilege = true;
            foreach (Stronghold stronghold in this.strongholds)
                if (stronghold.Area.Contains(blockSel.Position))
                {

                    area = stronghold;

                    if (stronghold.contested) return false;
                    if (stronghold.PlayerUID == null) return true;
                    if (stronghold.PlayerUID == byPlayer.PlayerUID) return true;
                    if (!(byPlayer.Groups?.Any(group => group.GroupUid == stronghold.GroupUID) ?? false))
                        return false;

                } // foreach ..

            return privilege;

        } // bool ..
    } // class ..
} // namespace ..
