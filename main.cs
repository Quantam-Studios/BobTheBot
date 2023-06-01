// For Discord bot
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;

public static class Bot
{
    public static readonly DiscordSocketClient Client = new(new DiscordSocketConfig()
    {
        GatewayIntents = GatewayIntents.Guilds
    });

    private static InteractionService Service;

    private static readonly string Token = Config.GetToken();

    public static async Task Main()
    {

        if (Token is null) throw new Exception("Discord bot token not set properly.");

        Client.Ready += Ready;
        Client.Log += Log;

        await Client.LoginAsync(TokenType.Bot, Token);
        await Client.StartAsync();

        while (Console.ReadKey().Key != ConsoleKey.Q) { };
    }

    private static Timer timer;

    private static async Task Ready()
    {
        Service = new InteractionService(Client, new InteractionServiceConfig()
        {
            UseCompiledLambda = true,
            ThrowOnError = true
        });

        await Service.AddModulesAsync(Assembly.GetEntryAssembly(), null);
        await Service.RegisterCommandsGloballyAsync();

        Client.SlashCommandExecuted += SlashCommandExecuted;
        Service.SlashCommandExecuted += SlashCommandResulted;

        string[] statuses = { "/help | Fonts!", "/help | New Commands!", "/help | RNG!", "/help | New Ideas!", "/help | 1,500+ users" };
        int index = 0;

        timer = new Timer(async x =>
        {
            await Client.SetGameAsync(statuses[index], null, ActivityType.Playing);
            index = index + 1 == statuses.Length ? 0 : index + 1;
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(16));

        // Update Top.GG stats.
        if (Token != Config.GetTestToken())
        {
            TopGG topGG = new TopGG();
            await topGG.PostStats();
            Console.WriteLine("Top.GG stats updated");
        }
        else
        {
            Console.WriteLine("Top.GG stats NOT updated because test bot is in use.");
        }

        // Print the servers bob is in.
        int totalUsers = 0;
        foreach (var guild in Bot.Client.Guilds)
        {
            Console.WriteLine($"{guild.Name}, {guild.MemberCount}");
            totalUsers += guild.MemberCount;
        }

        Console.WriteLine($"Total Users: {totalUsers}");

        var cpuUsage = await Performance.GetCpuUsageForProcess();
        Console.WriteLine("CPU at Ready: " + cpuUsage.ToString());
        var ramUsage = Performance.GetRamUsageForProcess();
        Console.WriteLine("RAM at Ready: " + ramUsage.ToString());
    }

    private static async Task SlashCommandExecuted(SocketSlashCommand command)
    {
        try
        {
            SocketInteractionContext ctx = new(Client, command);
            IResult res = await Service.ExecuteCommandAsync(ctx, null);
        }
        catch
        {
            if (command.Type == InteractionType.ApplicationCommand)
                await command.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
        }

        var cpuUsage = await Performance.GetCpuUsageForProcess();
        var ramUsage = Performance.GetRamUsageForProcess();
        var Location = command.GuildId == null ? "a DM" : Client.GetGuild(ulong.Parse(command.GuildId.ToString())).ToString();
        Console.WriteLine($"{DateTime.Now:dd/MM. H:mm:ss} CPU: {cpuUsage.ToString()} RAM: {ramUsage.ToString()} Location: {Location} Command: /{command.CommandName}");
    }

    private static async Task SlashCommandResulted(SlashCommandInfo info, IInteractionContext ctx, IResult res)
    {
        if (!res.IsSuccess)
        {
            switch (res.Error)
            {
                case InteractionCommandError.UnmetPrecondition:
                    await ctx.Interaction.FollowupAsync($"❌ Unmet Precondition: {res.ErrorReason}");
                    break;
                case InteractionCommandError.UnknownCommand:
                    await ctx.Interaction.FollowupAsync("❌ Unknown command");
                    break;
                case InteractionCommandError.BadArgs:
                    await ctx.Interaction.FollowupAsync("❌ Invalid number or arguments");
                    break;
                case InteractionCommandError.Exception:
                    await ctx.Interaction.FollowupAsync($"❌ Command exception: {res.ErrorReason}");
                    await ctx.Interaction.FollowupAsync("This might be because the server IP needs to changed.");
                    break;
                case InteractionCommandError.Unsuccessful:
                    await ctx.Interaction.FollowupAsync("❌ Command could not be executed");
                    break;
                default:
                    break;
            }
        }
    }

    private static Task Log(LogMessage logMessage)
    {
        Console.ForegroundColor = SeverityToConsoleColor(logMessage.Severity);
        Console.WriteLine($"{DateTime.Now:dd/MM. H:mm:ss} [{logMessage.Source}] {logMessage.Message}");
        Console.ResetColor();

        return Task.CompletedTask;
    }

    private static ConsoleColor SeverityToConsoleColor(LogSeverity severity)
    {
        return severity switch
        {
            LogSeverity.Critical => ConsoleColor.Red,
            LogSeverity.Debug => ConsoleColor.Blue,
            LogSeverity.Error => ConsoleColor.Yellow,
            LogSeverity.Info => ConsoleColor.Cyan,
            LogSeverity.Verbose => ConsoleColor.Green,
            LogSeverity.Warning => ConsoleColor.Magenta,
            _ => ConsoleColor.White,
        };
    }
}