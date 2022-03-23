using EmpyrionNetAPIDefinitions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace EmpyrionBackpackExtender
{
    public class ItemData
    {
        public int Id { get; set; }
        public int Count { get; set; }
        public string ItemName { get; set; }
    }
    public class BackpackConfiguration
    {
        public string ChatCommand { get; set; }
        public int MaxBackpacks { get; set; }
        public int Price { get; set; }
        public int OpenCooldownSecTimer { get; set; }
        public bool AllowSuperstack { get; set; }
        public string[] AllowedPlayfields { get; set; } = new string[] { };
        public string[] ForbiddenPlayfields { get; set; } = new string[] { };
        public string FilenamePattern { get; set; }
        public ItemData[] ForbiddenItems { get; set; } = new ItemData[] { new ItemData() };
        public ItemData[] AllowedItems { get; set; } = new ItemData[] { };
    }

    public class BackpackExtenderConfiguration
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public LogLevel LogLevel { get; set; } = LogLevel.Message;
        public string ChatCommandPrefix { get; set; } = "/\\";
        public string NameIdMappingFile { get; set; } = "filepath to the NameIdMapping.json e.g. from EmpyrionScripting for cross savegame support";
        public int MAX_STACK_SIZE { get; set; } = 16777215 - 1;

        public BackpackConfiguration PersonalBackpack { get; set; } = new BackpackConfiguration() {
            MaxBackpacks = 2,
            Price = 500000,
            ChatCommand = "vb",
            OpenCooldownSecTimer = 1 * 60,
            FilenamePattern = @"Personal\{0}.json"
        };
        public BackpackConfiguration FactionBackpack { get; set; } = new BackpackConfiguration()
        {
            MaxBackpacks = 2,
            Price = 1000000,
            ChatCommand = "fb",
            OpenCooldownSecTimer = 10 * 60,
            FilenamePattern = @"Faction\{0}.json"
        };
        public BackpackConfiguration OriginBackpack { get; set; } = new BackpackConfiguration()
        {
            MaxBackpacks = 1,
            Price = 0,
            ChatCommand = "ob",
            OpenCooldownSecTimer = 30 * 60,
            FilenamePattern = @"Origin\{0}.json"
        };
        public BackpackConfiguration GlobalBackpack { get; set; } = new BackpackConfiguration()
        {
            MaxBackpacks = 1,
            Price = 0,
            ChatCommand = "gb",
            OpenCooldownSecTimer = 30 * 60,
            FilenamePattern = @"Global\{0}.json"
        };
    }
}
