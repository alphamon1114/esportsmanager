using System;
namespace FpsManager
{
    // Persistent inventory. Automatic matches purchase through MatchEconomy.
    [Serializable] public class PlayerMatchState
    {
        public int credits = 800;
        public float armor; public bool helmet;
        public string[] equipment = { "ak_47", "flash", "smoke" };
        public static readonly string[] Consumables = { "flash", "smoke" };
        // Legacy isolated-round checks reissue grenades. Automatic matches buy them. Without this they
        // are only ever spent: there is no economy to rebuy from yet, so after a handful
        // of rounds nobody has utility left and the throws simply stop happening.
        // Weapons and credits are match state and are left alone.
        public void RestockUtility()
        {
            var kept = new System.Collections.Generic.List<string>();
            foreach (var item in equipment) if (Array.IndexOf(Consumables, item) < 0) kept.Add(item);
            kept.AddRange(Consumables);
            equipment = kept.ToArray();
        }
    }
    [Serializable] public class StatBlock { public int aim, utility, movement, charisma, composure; }
    [Serializable] public class Proficiency { public string weapon; public int stars; }
    [Serializable] public class PlayerData
    {
        public string id, teamId, handle, weaponPosition, riflerRole;
        public StatBlock stats;
        public Proficiency[] weapons;
    }
    [Serializable] public class TeamData { public string id, name, iglPlayerId; }
    [Serializable] public class Database { public TeamData[] teams; public PlayerData[] players; }

}
