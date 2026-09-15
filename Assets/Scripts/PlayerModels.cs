using System;
namespace FpsManager
{
    // Persistent match inventory. Purchasing and weapon selection are not wired yet.
    [Serializable] public class PlayerMatchState
    {
        public int credits = 800;
        public string[] equipment = { "ak_47" };
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
