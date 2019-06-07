using Eleon.Modding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmpyrionBackpackExtender
{
    public class BackpackItems
    {
        public ItemStack[] Items { get; set; }
    }
    public class BackpackData
    {
        public string OpendBySteamId { get; set; }
        public string OpendByName { get; set; }
        public int LastUsed { get; set; }
        public BackpackItems[] Backpacks { get; set; } = new BackpackItems[] { };
    }
}
