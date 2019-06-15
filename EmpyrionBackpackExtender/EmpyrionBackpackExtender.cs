using Eleon.Modding;
using System.Collections.Generic;
using System.Linq;
using EmpyrionNetAPIAccess;
using EmpyrionNetAPIDefinitions;
using EmpyrionNetAPITools;
using System.IO;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace EmpyrionBackpackExtender
{
    public class EmpyrionBackpackExtender : EmpyrionModBase
    {
        public ConfigurationManager<BackpackExtenderConfiguration> Configuration { get; set; }
        public ModGameAPI DediAPI { get; private set; }
        public ConcurrentDictionary<string, DateTime> BackPackLastOpend { get; private set; } = new ConcurrentDictionary<string, DateTime>();

        enum ChatType
        {
            Faction = 3,
            Global = 5,
        }

        public EmpyrionBackpackExtender()
        {
            EmpyrionConfiguration.ModName = "EmpyrionBackpackExtender";
        }

        public override void Initialize(ModGameAPI dediAPI)
        {
            DediAPI = dediAPI;
            LogLevel = LogLevel.Message;

            log($"**EmpyrionBackpackExtender: loaded");

            LoadConfiuration();
            LogLevel = Configuration.Current.LogLevel;
            ChatCommandManager.CommandPrefix = Configuration.Current.ChatCommandPrefix;

            AddCommandsFor(Configuration.Current.PersonalBackpack, "personal",  P => P.steamId);
            AddCommandsFor(Configuration.Current.FactionBackpack,  "faction",   P => P.factionId.ToString());
            AddCommandsFor(Configuration.Current.OriginBackpack,   "origin",    P => P.origin.ToString());
            AddCommandsFor(Configuration.Current.GlobalBackpack,   "global",    P => "global");
        }

        private void AddCommandsFor(BackpackConfiguration config, string name, Func<PlayerInfo, string> idFunc)
        {
            if (config.MaxBackpacks == 0) return;

                                        ChatCommands.Add(new ChatCommand($"{config.ChatCommand} help",               (I, A) => DisplayHelp (I, config, name, idFunc), $"help and commands for the {name} backpack"));
            if(config.MaxBackpacks > 1) ChatCommands.Add(new ChatCommand($"{config.ChatCommand} (?<number>\\d+)",    (I, A) => OpenBackpack(I, A, config, name, idFunc), $"Open the <N> = 1, 2, 3,... {name} backpack"));
                                        ChatCommands.Add(new ChatCommand($"{config.ChatCommand}",                    (I, A) => OpenBackpack(I, A, config, name, idFunc), $"Open the current {name} backpack"));
            if(config.MaxBackpacks > 1) ChatCommands.Add(new ChatCommand($"{config.ChatCommand} buy",                (I, A) => BuyBackpack(I, A, config, name, idFunc), $"buy another {name} backpack"));
        }

        private async Task BuyBackpack(ChatInfo info, Dictionary<string, string> args, BackpackConfiguration config, string name, Func<PlayerInfo, string> idFunc)
        {
            var P = await Request_Player_Info(info.playerId.ToId());
            ConfigurationManager<BackpackData> currentBackpack = new ConfigurationManager<BackpackData>()
            {
                ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, String.Format(config.FilenamePattern, idFunc(P)))
            };
            currentBackpack.Load();

            if (currentBackpack.Current.Backpacks.Length >= config.MaxBackpacks)
            {
                MessagePlayer(info.playerId, $"max allowed backpacks #{config.MaxBackpacks} reached");
                return;
            }

            if (config.Price > 0 && P.credits < config.Price)
            {
                MessagePlayer(info.playerId, $"you dont have {config.Price} credits for a new backpack");
                return;
            }

            var answer = await ShowDialog(info.playerId, P, $"Are you sure you want to buy a {name} backpack", $"[c][00ff00]\"{name}\"[-][/c] backpack for [c][ffffff]{config.Price}[-][/c] Credits?", "Yes", "No");
            if (answer.Id != P.entityId || answer.Value != 0) return;

            currentBackpack.Current.Backpacks = currentBackpack.Current.Backpacks.Concat(new[] { new BackpackItems() }).ToArray();

            currentBackpack.Save();

            await Request_Player_SetCredits(new IdCredits(P.entityId, P.credits - config.Price));
        }

        private void LoadConfiuration()
        {
            Configuration = new ConfigurationManager<BackpackExtenderConfiguration>
            {
                ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, @"Configuration.json")
            };

            Configuration.Load();
            Configuration.Save();
        }

        public void MessagePlayer(int id, string message)
        {
            var outMsg = new IdMsgPrio()
            {
                id = id,
                msg = message
            };
            Request_InGameMessage_SinglePlayer(outMsg);
        }

        private async Task OpenBackpack(ChatInfo info, Dictionary<string, string> args, BackpackConfiguration config, string name, Func<PlayerInfo, string> getConfigFileId)
        {
            log($"**OpenBackpack {info.type}:{info.msg} {args.Aggregate("", (s, i) => s + i.Key + "/" + i.Value + " ")}");

            if (info.type == (byte)ChatType.Faction) return;

            var P = await Request_Player_Info(info.playerId.ToId());

            if(BackPackLastOpend.TryGetValue($"{P.steamId}{name}", out var time) && (DateTime.Now - time).TotalSeconds <= config.OpenCooldownSecTimer)
            {
                MessagePlayer(info.playerId, $"backpack open cooldown please wait {(new TimeSpan(0, 0, config.OpenCooldownSecTimer) - (DateTime.Now - time)).ToString(@"hh\:mm\:ss")}");
                return;
            }

            ConfigurationManager <BackpackData> currentBackpack = new ConfigurationManager<BackpackData>() {
                ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, String.Format(config.FilenamePattern, getConfigFileId(P)))
            };
            currentBackpack.Load();

            if (!string.IsNullOrEmpty(currentBackpack.Current.OpendBySteamId) &&
                currentBackpack.Current.OpendBySteamId != P.steamId)
            {
                var onlinePlayers = await Request_Player_List();

                MessagePlayer(info.playerId, $"backpack currently opend by {currentBackpack.Current.OpendByName}");
                return;
            }

            if(config.ForbiddenPlayfields.Length > 0 && config.ForbiddenPlayfields.Contains(P.playfield))
            {
                MessagePlayer(info.playerId, $"backpacks are not allowed on this playfield");
                return;
            }

            if (config.AllowedPlayfields.Length > 0 && !config.AllowedPlayfields.Contains(P.playfield))
            {
                MessagePlayer(info.playerId, $"backpacks are not allowed on this playfield");
                return;
            }

            int usedBackpackNo = currentBackpack.Current.LastUsed;
            if(args.TryGetValue("number", out string numberArgs)) int.TryParse(numberArgs, out usedBackpackNo);
            usedBackpackNo = Math.Max(1, usedBackpackNo);

            if (usedBackpackNo > config.MaxBackpacks)
            {
                MessagePlayer(info.playerId, $"max allowed backpacks #{config.MaxBackpacks}");
                return;
            }

            if (currentBackpack.Current.Backpacks.Length < usedBackpackNo)
            {
                if (config.Price > 0)
                {
                    MessagePlayer(info.playerId, $"you have only {currentBackpack.Current.Backpacks.Length} backpack(s) please buy one");
                    return;
                }

                var list = currentBackpack.Current.Backpacks?.ToList() ?? new List<BackpackItems>();
                for (int i = list.Count; i < usedBackpackNo; i++) list.Add(new BackpackItems() { Items = new ItemStack[] { } });
                currentBackpack.Current.Backpacks = list.ToArray();
            }

            currentBackpack.Current.LastUsed        = usedBackpackNo;
            currentBackpack.Current.OpendByName     = P.playerName;
            currentBackpack.Current.OpendBySteamId  = P.steamId;
            currentBackpack.Save();

            Action<ItemExchangeInfo> eventCallback = null;
            eventCallback = (B) =>
            {
                if (P.entityId != B.id) return;

                Event_Player_ItemExchange -= eventCallback;
                EmpyrionBackpackExtender_Event_Player_ItemExchange(B, P, currentBackpack, config, usedBackpackNo);
            };

            Event_Player_ItemExchange += eventCallback;
            BackPackLastOpend.AddOrUpdate($"{P.steamId}{name}", DateTime.Now, (S, D) => DateTime.Now);

            var exchange = new ItemExchangeInfo()
            {
                buttonText  = "close",
                desc        = "",
                id          = info.playerId,
                items       = currentBackpack.Current.Backpacks[usedBackpackNo - 1].Items ?? new ItemStack[] { },
                title       = $"Backpack ({name}) {(config.MaxBackpacks > 1 ? "#" + currentBackpack.Current.LastUsed : string.Empty)}"
            };

            try   { await Request_Player_ItemExchange(0, exchange); } // ignore Timeout Exception
            catch { }
        }

        private void EmpyrionBackpackExtender_Event_Player_ItemExchange(
            ItemExchangeInfo                   bpData, 
            PlayerInfo                         P, 
            ConfigurationManager<BackpackData> currentBackpack,
            BackpackConfiguration              config,
            int                                usedBackpackNo)
        {
            currentBackpack.Current.Backpacks[usedBackpackNo - 1].Items = config.AllowSuperstack ? SuperstackItems(bpData.items ?? new ItemStack[] { }) : bpData.items ?? new ItemStack[] { };
            currentBackpack.Current.OpendByName = null;
            currentBackpack.Current.OpendBySteamId = null;
            currentBackpack.Save();
        }

        private ItemStack[] SuperstackItems(ItemStack[] itemStack)
        {
            var result = new Dictionary<int, ItemStack>();
            Array.ForEach(itemStack,
                I => {
                    if (result.TryGetValue(I.id, out ItemStack found)) result[I.id] = new ItemStack(I.id, result[I.id].count + I.count);
                    else                                               result.Add(I.id, I);
                });

            return result.Values.ToArray();
        }

        private async Task DisplayHelp(ChatInfo info, BackpackConfiguration config, string name, Func<PlayerInfo, string> idFunc)
        {
            var currentBackpackLength = 0;

            if (config.Price > 0)
            {
                var P = await Request_Player_Info(info.playerId.ToId());
                ConfigurationManager<BackpackData> currentBackpack = new ConfigurationManager<BackpackData>()
                {
                    ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, String.Format(config.FilenamePattern, idFunc(P)))
                };
                currentBackpack.Load();

                currentBackpackLength = currentBackpack.Current.Backpacks.Length;
            }

            await DisplayHelp(info.playerId,
                $"max allowed {name} backpacks: {config.MaxBackpacks}\n" +
                (config.Price > 0 ? $"price per {name} backpack: {config.Price}\nyou have {currentBackpackLength} {name} backpack(s)\n" : "") +
                PlayfieldList("\nallowed playfields:",   config.AllowedPlayfields) +
                PlayfieldList("\nforbidden playfields:", config.ForbiddenPlayfields)
            );
        }

        private string PlayfieldList(string title, string[] playfields)
        {
            return playfields.Length == 0 ? "" : playfields.Aggregate(title, (S, P) => S + "\n" + P);
        }
    }
}
