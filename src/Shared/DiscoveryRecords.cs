using System;
using System.Collections.Generic;
using System.Linq;

namespace SteamManagerProtocol
{
    public static class DiscoveryRecords
    {
        public static int Apply(IEnumerable<string> names,HashSet<string> known,IEnumerable<Dictionary<string,float>> pickupRecords)
        {
            var records=pickupRecords.Distinct().ToArray();int changed=0;
            foreach(string name in names.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct())
            {
                bool added=known.Add(name);
                foreach(var record in records){float count;if(!record.TryGetValue(name,out count)||count<1){record[name]=1;added=true;}}
                if(added)changed++;
            }
            return changed;
        }
    }
}
