namespace EmpyrionBackpackExtender
{
    public class ItemNameStack
    {
        public int id;
        public string name;
        public int count;
        public byte slotIdx;
        public int ammo;
        public int decay;
    }
    public class BackpackItems
    {
        public ItemNameStack[] Items { get; set; }
    }
    public class BackpackData
    {
        public string OpendBySteamId { get; set; }
        public string OpendByName { get; set; }
        public int LastUsed { get; set; }
        public string LastAccessPlayerName { get; set; }
        public string LastAccessFactionName { get; set; }
        public BackpackItems[] Backpacks { get; set; } = new BackpackItems[] { };
    }
}
