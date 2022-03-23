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
using Newtonsoft.Json;
using System.Collections;

namespace EmpyrionBackpackExtender
{
    public class EmpyrionBackpackExtender : EmpyrionModBase
    {
        public ConfigurationManager<BackpackExtenderConfiguration> Configuration { get; set; }
        public ModGameAPI DediAPI { get; private set; }
        public ConcurrentDictionary<string, DateTime> BackPackLastOpend { get; private set; } = new ConcurrentDictionary<string, DateTime>();
        public FactionInfoList CurrentFactions { get; set; }

        public IReadOnlyDictionary<string, int> BlockNameIdMapping
        {
            get {
                if (_BlockNameIdMapping == null && File.Exists(Configuration.Current.NameIdMappingFile ?? string.Empty))
                    Log($"NameIdMapping:'{Configuration.Current.NameIdMappingFile}' CurrentDirectory:{Directory.GetCurrentDirectory()}", LogLevel.Message);
                    try{ _BlockNameIdMapping = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(Configuration.Current.NameIdMappingFile)); }
                    catch (Exception error) { Log($"NameIdMapping read failed:{error}", LogLevel.Error); }

                return _BlockNameIdMapping;
            }
        }
        IReadOnlyDictionary<string, int> _BlockNameIdMapping;

        public IReadOnlyDictionary<int, string> BlockIdNameMapping
        {
            get {
                if (_BlockIdNameMapping == null)
                    try { _BlockIdNameMapping = BlockNameIdMapping.ToDictionary(b => b.Value, b => b.Key); }
                    catch (Exception error) { Console.WriteLine(error); }

                return _BlockIdNameMapping;
            }
        }
         IReadOnlyDictionary<int, string> _BlockIdNameMapping;

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
            string playerName = null;
            try { 
                Log($"**OpenBackpack {info.type}:{info.msg} {args.Aggregate("", (s, i) => s + i.Key + "/" + i.Value + " ")}");

                if (info.type == (byte)ChatType.Faction) return;

                var P = await Request_Player_Info(info.playerId.ToId());
                playerName = P.playerName;

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
                bool isBackpackOpenOkResult = true;
                eventCallback = (B) =>
                {
                    if (P.entityId != B.id) return;
                    
                    if(isBackpackOpenOkResult)
                    {
                        isBackpackOpenOkResult = false;
                        return;
                    }

                    if (ItemStacksOk(config, B.items, out var errorMsg))
                    {
                        Event_Player_ItemExchange -= eventCallback;
                        EmpyrionBackpackExtender_Event_Player_ItemExchange(B, currentBackpack, config, usedBackpackNo);
                    }
                    else
                    {
                        isBackpackOpenOkResult = true;
                        OpenBackpackItemExcange(info.playerId, config, name, $"Not allowed:{errorMsg}", currentBackpack, B.items).GetAwaiter().GetResult();
                    }
                };

                Event_Player_ItemExchange += eventCallback;
                BackPackLastOpend.AddOrUpdate($"{P.steamId}{name}", DateTime.Now, (S, D) => DateTime.Now);

                await OpenBackpackItemExcange(info.playerId, config, name, "", currentBackpack, currentBackpack.Current.Backpacks[usedBackpackNo - 1].Items?.Select(i => Convert(i)).ToArray() ?? new ItemStack[] { });
            }
            catch (Exception error) {
                Log($"backpack open failed for player '{playerName}'/{info.playerId} :{error}", LogLevel.Error);
                MessagePlayer(info.playerId, $"backpack open failed {error}"); 
            }
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

        private bool ItemStacksOk(BackpackConfiguration config, ItemStack[] items, out string errorMsg)
        {
            errorMsg = string.Empty;
            if(items == null) return true;

            var flattenItems = new Dictionary<int, int>();
            items.ToList().ForEach(item => {
                if(flattenItems.TryGetValue(item.id, out int count)) flattenItems[item.id] += item.count;
                else                                                 flattenItems.Add(item.id, item.count);
            });

            if (config.ForbiddenItems != null && config.ForbiddenItems.Length > 0) errorMsg = config.ForbiddenItems?.Aggregate(errorMsg, (msg, I) => flattenItems.       Any(i => i.Key == I.Id && i.Value  > I.Count) ? msg + $"({I.ItemName} > {I.Count}) " : msg);
            if (config.AllowedItems   != null && config.AllowedItems  .Length > 0) errorMsg = flattenItems          .Aggregate(errorMsg, (msg, I) => config.AllowedItems.Any(i => I.Key == i.Id && I.Value <= i.Count) ? msg : msg + $"({(BlockIdNameMapping?.TryGetValue(I.Key, out var name) == true ? $"{name} [{I.Key}]" : I.Key.ToString())} > {config.AllowedItems.FirstOrDefault(i => i.Id == I.Key)?.Count ?? 0}) ");
            
            return string.IsNullOrEmpty(errorMsg);
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
            catch (Exception error) {
                Log($"backpack open failed for player {playerId} :{error}", LogLevel.Error);
                MessagePlayer(playerId, $"backpack open failed {error}");
            }
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
            var result = new List<ItemNameStack>();
            Array.ForEach(itemStack,
                I => {
                    var found = result.FirstOrDefault(i => i.id == I.id && (i.count + I.count) <= Configuration.Current.MAX_STACK_SIZE);
                    if (found != null && (found.count > 1 || IsStackableItem(I))) found.count += I.count;
                    else                                                          result.Add(I);
                });

            return result.ToArray();
        }

        private bool IsStackableItem(ItemNameStack i) => i.ammo == 0 && i.decay == 0 && i.count > 1; // Nur Items die schon stackbar sind und ohne Verfallszeit oder Munition gerechnet werden

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
                $"max allowed {name} {(config.AllowSuperstack ? "superstack backpacks" : "backpacks")}: {config.MaxBackpacks}\n" +
                (config.Price > 0 ? $"price per {name} backpack: {config.Price}\nyou have {currentBackpackLength} {name} backpack(s)\n" : "") +
                PlayfieldList("\nallowed playfields:",   config.AllowedPlayfields) +
                PlayfieldList("\nforbidden playfields:", config.ForbiddenPlayfields) +
                (config.ForbiddenItems != null && config.ForbiddenItems.Length > 0 ? $"\nnot allowed:{config.ForbiddenItems?.Aggregate("", (s, i) => s + $" ({i.ItemName} > {i.Count})")}" : "") +
                (config.AllowedItems   != null && config.AllowedItems  .Length > 0 ? $"\nonly allowed:{config.AllowedItems?.Aggregate("", (s, i) => s + $" ({i.ItemName} <= {i.Count})")}" : "")
            );
        }

        private string PlayfieldList(string title, string[] playfields)
        {
            return playfields.Length == 0 ? "" : playfields.Aggregate(title, (S, P) => S + "\n" + P);
        }
    }
}
