using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;
using DevConsole;
using DevConsole.Commands;
using RWCustom;
using UnityEngine;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace ReputationEditor;

[BepInPlugin("alduris.reputation", "Reputation Editor", "1.0"), BepInDependency("slime-cubed.devconsole", BepInDependency.DependencyFlags.HardDependency)]
sealed class Plugin : BaseUnityPlugin
{
    public static new ManualLogSource Logger;
    bool IsInit;

    public void OnEnable()
    {
        Logger = base.Logger;
        On.RainWorld.OnModsInit += OnModsInit;
    }

    private void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        if (IsInit) return;
        IsInit = true;

        new CommandBuilder("reputation")
            .RunGame(ReputationRunGame)
            .AutoComplete(ReputationAutoComplete)
            .Help("reputation [get|set] [community] [region?] [player?] [value?]")
            .Register();
    }

    private void ReputationRunGame(RainWorldGame game, string[] args)
    {
        // Basic error checking
        if (args.Length < 2)
        {
            GameConsole.WriteLine("Not enough arguments!");
            return;
        }
        if (args[0] != "get" && args[0] != "set")
        {
            GameConsole.WriteLine("Invalid operation! Must be either `get` or `set`");
            return;
        }
        var community = new CreatureCommunities.CommunityID(args[1]);
        if (community.Index <= 0)
        {
            GameConsole.WriteLine("Invalid creature community!");
            return;
        }
        if (game.session?.creatureCommunities?.playerOpinions == null)
        {
            GameConsole.WriteLine("Not in a valid game session");
            return;
        }

        // More specific actions
        int regionIndex = 0;
        if (args.Length >= 3)
        {
            // As much as I would like to use IndexOf, it seems to only work by reference rather than string equality so alas here we are
            var regionNames = game.rainWorld.progression.regionNames;
            for (int i = 0; i < regionNames.Length; i++)
            {
                if (regionNames[i].Equals(args[2], System.StringComparison.InvariantCultureIgnoreCase))
                {
                    regionIndex = i + 1;
                    break;
                }
            }
        }

        ref float playerOpinionRef = ref game.session.creatureCommunities.playerOpinions[community.Index - 1, regionIndex, 0];
        if (args[0] == "get")
        {
            GameConsole.WriteLine((playerOpinionRef * 100f).ToString("n1"));
        }
        else
        {
            if (!float.TryParse(args.Last(), out float amount))
            {
                GameConsole.WriteLine("Invalid value!");
                return;
            }
            amount = Mathf.Clamp(amount, -100f, 100f) / 100f;
            playerOpinionRef = amount;
        }
    }

    private IEnumerable<string> ReputationAutoComplete(string[] args)
    {
        switch (args.Length)
        {
            case 0:
                yield return "get";
                yield return "set";
                break;
            case 1:
                foreach (string type in CreatureCommunities.CommunityID.values.entries)
                {
                    if (type == "None") continue;
                    yield return type;
                }
                break;
            case 2:
                if (args[0] == "set")
                {
                    yield return "help-value: float";
                }
                if (Custom.rainWorld.progression?.regionNames != null)
                {
                    foreach (string region in Custom.rainWorld.progression.regionNames)
                    {
                        yield return region;
                    }
                }
                break;
            case 3:
                if (args[0] == "set" && !float.TryParse(args[2], out _))
                {
                    yield return "help-value: float";
                }
                break;
        }
        yield break;
    }
}
