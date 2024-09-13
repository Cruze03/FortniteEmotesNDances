using System.Collections.Concurrent;
using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
using CounterStrikeSharp.API.Modules.Entities;
using Microsoft.Extensions.Logging;

namespace FortniteEmotes;
public partial class Plugin
{
    /*
        * Command Manager
        * Credits of this whole command manager goes to K4's Zenith plugin
    */
    private readonly ConcurrentDictionary<string, List<CommandDefinition>> _pluginCommands = [];
    private readonly ConcurrentDictionary<string, string> _commandPermissions = [];

    public void RegisterCommand(string command, string description, CommandInfo.CommandCallback handler, CommandUsage usage = CommandUsage.CLIENT_AND_SERVER, int argCount = 0, string? helpText = null, string? permission = null)
    {
        if (!command.StartsWith("css_"))
            command = "css_" + command;

        string callingPlugin = CallerIdentifier.GetCallingPluginName();

        var existingCommand = _pluginCommands
            .SelectMany(kvp => kvp.Value.Select(cmd => new { Plugin = kvp.Key, Command = cmd }))
            .FirstOrDefault(x => x.Command.Name == command);

        if (existingCommand != null)
        {
            if (existingCommand.Plugin != callingPlugin)
            {
                Logger.LogError($"Command '{command}' is already registered by plugin '{existingCommand.Plugin}'. Registration by '{callingPlugin}' is not allowed.");
                return;
            }
            else
            {
                CommandManager.RemoveCommand(existingCommand.Command);
                _pluginCommands[callingPlugin].Remove(existingCommand.Command);
                Logger.LogWarning($"Command '{command}' already exists for plugin '{callingPlugin}', overwriting.");
            }
        }

        if (!_pluginCommands.ContainsKey(callingPlugin))
            _pluginCommands[callingPlugin] = [];

        var newCommand = new CommandDefinition(command, description, (controller, info) =>
        {
            if (!CommandHelper(controller, info, usage, argCount, helpText, permission))
                return;

            handler(controller, info);
        });

        // ? Using CommandManager due to AddCommand cannot unregister modular commands
        CommandManager.RegisterCommand(newCommand);
        _pluginCommands[callingPlugin].Add(newCommand);
        _commandPermissions[command] = permission ?? string.Empty;
    }

    public void RemoveAllCommands()
    {
        foreach (var pluginEntry in _pluginCommands)
        {
            foreach (var command in pluginEntry.Value)
            {
                CommandManager.RemoveCommand(command);
            }
        }

        _pluginCommands.Clear();
        _commandPermissions.Clear();
    }

    public bool CommandHelper(CCSPlayerController? player, CommandInfo info, CommandUsage usage, int argCount = 0, string? helpText = null, string? permission = null)
    {
        // Player? player = Player.Find(controller);

        switch (usage)
        {
            case CommandUsage.CLIENT_ONLY:
                if (player == null || !player.IsValid)
                {
                    info.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer["emote.command.client-only"]}");
                    return false;
                }
                break;
            case CommandUsage.SERVER_ONLY:
                if (player != null)
                {
                    info.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer["emote.command.server-only"]}");
                    return false;
                }
                break;
        }

        if (permission != null && permission.Length > 0 && !permission.Equals("none", StringComparison.CurrentCultureIgnoreCase))
        {
            if (player != null && !AdminManager.PlayerHasPermissions(player, permission) && !AdminManager.PlayerHasCommandOverride(player, @info.GetArg(0)))
            {
                info.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer["emote.command.no-permission"]}");
                return false;
            }
        }

        if (argCount > 0 && helpText != null)
        {
            int checkArgCount = argCount + 1;
            if (info.ArgCount < checkArgCount)
            {
                info.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer["emote.command.help", info.ArgByIndex(0), helpText]}");
                return false;
            }
        }

        return true;
    }

    public static bool CanTarget(CCSPlayerController? player, CCSPlayerController? target)
	{
		if (player is null || target is null) return true;
		if (target.IsBot) return true;

		return AdminManager.CanPlayerTarget(player, target)
        || AdminManager.CanPlayerTarget(new SteamID(player.SteamID), new SteamID(target.SteamID));
	}

    private static TargetResult? GetTarget(CommandInfo command)
	{
		var matches = command.GetArgTargetResult(1);

		if (!matches.Any())
		{
            return null;
		}

		return matches;
	}
}

public static class CallerIdentifier
{
	private static readonly string CurrentPluginName = Assembly.GetExecutingAssembly().GetName().Name!;
    private static readonly string[] BlockAssemblies = ["System.", "FortniteEmotesNDancesAPI", "KitsuneMenu"];

	public static string GetCallingPluginName()
	{
		var stackTrace = new System.Diagnostics.StackTrace(true);
		string callingPlugin = CurrentPluginName;

		for (int i = 1; i < stackTrace.FrameCount; i++)
		{
			var assembly = stackTrace.GetFrame(i)?.GetMethod()?.DeclaringType?.Assembly;
			var assemblyName = assembly?.GetName().Name;

			if (assemblyName == "CounterStrikeSharp.API")
				break;

			if (assemblyName != CurrentPluginName && assemblyName != null && !BlockAssemblies.Any(assemblyName.StartsWith))
			{
				callingPlugin = assemblyName;
				break;
			}
		}

		return callingPlugin;
	}
}