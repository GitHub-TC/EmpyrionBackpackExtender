using EmpyrionNetAPIDefinitions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace EmpyrionBackpackExtender
{
    public class BackpackConfiguration
    {
        public string ChatCommand { get; set; }
        public int MaxBackpacks { get; set; }
        public int Price { get; set; }
        public bool AllowSuperstack { get; set; }
        public string[] AllowedPlayfields { get; set; } = new string[] { };
        public string[] ForbiddenPlayfields { get; set; } = new string[] { };
        public string FilenamePattern { get; set; }
    }

    public class BackpackExtenderConfiguration
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public LogLevel LogLevel { get; set; } = LogLevel.Message;
        public string ChatCommandPrefix { get; set; } = "\\";
        public BackpackConfiguration PersonalBackpack { get; set; } = new BackpackConfiguration() {
            MaxBackpacks = 2,
            Price = 500000,
            ChatCommand = "vb",
            FilenamePattern = @"Personal\{0}.json"
        };
        public BackpackConfiguration FactionBackpack { get; set; } = new BackpackConfiguration()
        {
            MaxBackpacks = 2,
            Price = 1000000,
            ChatCommand = "fb",
            FilenamePattern = @"Faction\{0}.json"
        };
        public BackpackConfiguration OriginBackpack { get; set; } = new BackpackConfiguration()
        {
            MaxBackpacks = 1,
            Price = 0,
            ChatCommand = "ob",
            FilenamePattern = @"Origin\{0}.json"
        };
        public BackpackConfiguration GlobalBackpack { get; set; } = new BackpackConfiguration()
        {
            MaxBackpacks = 1,
            Price = 0,
            ChatCommand = "gb",
            FilenamePattern = @"Global\{0}.json"
        };
    }
}
