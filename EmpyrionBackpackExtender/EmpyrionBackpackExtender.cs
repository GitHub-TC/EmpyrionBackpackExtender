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
using EmpyrionNetAPITools.Extensions;

namespace EmpyrionBackpackExtender
{
    public class EmpyrionBackpackExtender : EmpyrionModBase
    {
        public ConfigurationManager<BackpackExtenderConfiguration> Configuration { get; set; }
        public ModGameAPI DediAPI { get; private set; }
        public ConcurrentDictionary<string, DateTime> BackPackLastOpend { get; private set; } = new ConcurrentDictionary<string, DateTime>();
        public FactionInfoList CurrentFactions { get; set; }

        public static IReadOnlyDictionary<string, int> BlockNameIdMapping
        {
            get {
                if (_BlockNameIdMapping == null)
                    try { _BlockNameIdMapping = ReadBlockMapping(Path.Combine(EmpyrionConfiguration.SaveGamePath, @"blocksmap.dat")); }
                    catch (Exception error) { Console.WriteLine(error); }

                return _BlockNameIdMapping;
            }
        }
        static IReadOnlyDictionary<string, int> _BlockNameIdMapping;

        public static IReadOnlyDictionary<int, string> BlockIdNameMapping
        {
            get {
                if (_BlockIdNameMapping == null)
                    try { _BlockIdNameMapping = BlockNameIdMapping.ToDictionary(b => b.Value, b => b.Key); }
                    catch (Exception error) { Console.WriteLine(error); }

                return _BlockIdNameMapping;
            }
        }
        static IReadOnlyDictionary<int, string> _BlockIdNameMapping;

        public static IReadOnlyDictionary<string, int> ReadBlockMapping(string filename)
        {
            if (!File.Exists(filename)) return null;

            var result = new ConcurrentDictionary<string, int>();

            var fileContent = File.ReadAllBytes(filename);
            for (var currentOffset = 9; currentOffset < fileContent.Length;)
            {
                var len = fileContent[currentOffset++];
                var name = System.Text.Encoding.ASCII.GetString(fileContent, currentOffset, len);
                currentOffset += len;

                var id = fileContent[currentOffset++] | fileContent[currentOffset++] << 8;

                result.AddOrUpdate(name, id, (s, i) => id);
            }

            return result;
        }




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

            Log($"**EmpyrionBackpackExtender: loaded");

            LoadConfiuration();
            LogLevel = Configuration.Current.LogLevel;
            ChatCommandManager.CommandPrefix = Configuration.Current.ChatCommandPrefix;

            TaskTools.Intervall(60000, () => CurrentFactions = Request_Get_Factions(0.ToId()).Result);

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
            try { 
                Log($"**OpenBackpack {info.type}:{info.msg} {args.Aggregate("", (s, i) => s + i.Key + "/" + i.Value + " ")}");

                if (info.type == (byte)ChatType.Faction) return;

                var P = await Request_Player_Info(info.playerId.ToId());

                if (BackPackLastOpend.TryGetValue($"{P.steamId}{name}", out var time) && (DateTime.Now - time).TotalSeconds <= config.OpenCooldownSecTimer)
                {
                    MessagePlayer(info.playerId, $"backpack open cooldown please wait {(new TimeSpan(0, 0, config.OpenCooldownSecTimer) - (DateTime.Now - time)).ToString(@"hh\:mm\:ss")}");
                    return;
                }

                ConfigurationManager<BackpackData> currentBackpack = new ConfigurationManager<BackpackData>()
                {
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

                if (config.ForbiddenPlayfields.Length > 0 && config.ForbiddenPlayfields.Contains(P.playfield))
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
                if (args.TryGetValue("number", out string numberArgs)) int.TryParse(numberArgs, out usedBackpackNo);
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
                    for (int i = list.Count; i < usedBackpackNo; i++) list.Add(new BackpackItems() { Items = new ItemNameStack[] { } });
                    currentBackpack.Current.Backpacks = list.ToArray();
                }

                currentBackpack.Current.LastUsed                = usedBackpackNo;
                currentBackpack.Current.OpendByName             = P.playerName;
                currentBackpack.Current.OpendBySteamId          = P.steamId;
                currentBackpack.Current.LastAccessPlayerName    = P.playerName;
                currentBackpack.Current.LastAccessFactionName   = CurrentFactions != null && CurrentFactions.factions != null ? CurrentFactions.factions.FirstOrDefault(F => F.factionId == P.factionId).abbrev : P.factionId.ToString();
                currentBackpack.Save();

                Action<ItemExchangeInfo> eventCallback = null;
                eventCallback = (B) =>
                {
                    if (P.entityId != B.id) return;
                
                    if (ContainsForbiddenItemStacks(config, B.items, out var errorMsg)) OpenBackpackItemExcange(info.playerId, config, name, "Not allowed:" + errorMsg, currentBackpack, B.items).GetAwaiter().GetResult();
                    else
                    {
                        Event_Player_ItemExchange -= eventCallback;
                        EmpyrionBackpackExtender_Event_Player_ItemExchange(B, currentBackpack, config, usedBackpackNo);
                    }
                };

                Event_Player_ItemExchange += eventCallback;
                BackPackLastOpend.AddOrUpdate($"{P.steamId}{name}", DateTime.Now, (S, D) => DateTime.Now);

                await OpenBackpackItemExcange(info.playerId, config, name, "", currentBackpack, currentBackpack.Current.Backpacks[usedBackpackNo - 1].Items?.Select(i => Convert(i)).ToArray() ?? new ItemStack[] { });
            }
            catch (Exception error) { MessagePlayer(info.playerId, $"backpack open failed {error}"); }
        }

        private ItemStack Convert(ItemNameStack i)
        {
            int id = i.id;
            return new ItemStack(i.name == null ? i.id : BlockNameIdMapping?.TryGetValue(i.name, out id) == true ? id : i.id, i.count)
            {
                ammo    = i.ammo,
                decay   = i.decay,
                slotIdx = i.slotIdx
            };
        }

        private ItemNameStack Convert(ItemStack i)
        {
            string name = null;
            return new ItemNameStack{
                id      = i.id,
                name    = BlockIdNameMapping?.TryGetValue(i.id, out name) == true ? name : null,
                count   = i.count,
                ammo    = i.ammo,
                decay   = i.decay,
                slotIdx = i.slotIdx
            };
        }

        private bool ContainsForbiddenItemStacks(BackpackConfiguration config, ItemStack[] items, out string errorMsg)
        {
            errorMsg = null;
            if (config.ForbiddenItems == null) return false;

            errorMsg = config.ForbiddenItems?.Aggregate("", (msg, I) => items?.Any(i => i.id == I.Id && i.count > I.Count) == true ? msg + $"({I.ItemName} > {I.Count}) " : msg);

            return !string.IsNullOrEmpty(errorMsg);
        }

        private async Task OpenBackpackItemExcange(int playerId, BackpackConfiguration config, string name, string description, ConfigurationManager<BackpackData> currentBackpack, ItemStack[] items)
        {
            var exchange = new ItemExchangeInfo()
            {
                buttonText  = "close",
                desc        = description,
                id          = playerId,
                items       = items ?? new ItemStack[] { },
                title       = $"Backpack ({name}) {(config.MaxBackpacks > 1 ? "#" + currentBackpack.Current.LastUsed : string.Empty)}"
            };

            try { await Request_Player_ItemExchange(Timeouts.NoResponse, exchange); } // ignore Timeout Exception
            catch (Exception error) { MessagePlayer(playerId, $"backpack open failed {error}");}
        }

        private void EmpyrionBackpackExtender_Event_Player_ItemExchange(
            ItemExchangeInfo                   bpData, 
            ConfigurationManager<BackpackData> currentBackpack,
            BackpackConfiguration              config,
            int                                usedBackpackNo)
        {
            currentBackpack.Current.Backpacks[usedBackpackNo - 1].Items = config.AllowSuperstack ? SuperstackItems(bpData.items?.Select(i => Convert(i)).ToArray() ?? new ItemNameStack[] { }) : bpData.items?.Select(i => Convert(i)).ToArray() ?? new ItemNameStack[] { };
            currentBackpack.Current.OpendByName = null;
            currentBackpack.Current.OpendBySteamId = null;
            currentBackpack.Save();
        }

        private ItemNameStack[] SuperstackItems(ItemNameStack[] itemStack)
        {
            var result = new Dictionary<int, ItemNameStack>();
            Array.ForEach(itemStack,
                I => {
                    if (result.TryGetValue(I.id, out ItemNameStack found)) result[I.id] = Convert(new ItemStack(I.id, result[I.id].count + I.count));
                    else                                                   result.Add(I.id, I);
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
                PlayfieldList("\nforbidden playfields:", config.ForbiddenPlayfields) +
                $"\nnot allowed:{config.ForbiddenItems?.Aggregate("", (s, i) => s + $" ({i.ItemName} > {i.Count})")}"
            );
        }

        private string PlayfieldList(string title, string[] playfields)
        {
            return playfields.Length == 0 ? "" : playfields.Aggregate(title, (S, P) => S + "\n" + P);
        }
    }
}
