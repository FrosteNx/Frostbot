using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System;
using System.Threading.Tasks;

namespace Frostbot.commands
{
    internal class TestCommands : BaseCommandModule
    {
        [Command("hello")] 
        public async Task MyFirstCommand(CommandContext ctx)
        {
            await ctx.Channel.SendMessageAsync($"Hello {ctx.User.Username}");
        }

        [Command("add")]
        public async Task Add(CommandContext ctx, double x, double y)
        {
            double res = x + y;
            await ctx.Channel.SendMessageAsync(res.ToString());
        }

        [Command("subtract")]
        public async Task Subtract(CommandContext ctx, double x, double y)
        {
            double res = x - y;
            await ctx.Channel.SendMessageAsync(res.ToString());
        }

        [Command("multiply")]
        public async Task Multiply(CommandContext ctx, double x, double y)
        {
            double res = x * y;
            await ctx.Channel.SendMessageAsync(res.ToString());
        }

        [Command("divide")]
        public async Task Divide(CommandContext ctx, double x, double y)
        {
            double res = x / y;
            await ctx.Channel.SendMessageAsync(res.ToString());
        }

        [Command("sqrt")]
        public async Task SquareRoot(CommandContext ctx, double x)
        {
            double res = Math.Sqrt(x);
            await ctx.Channel.SendMessageAsync(res.ToString());
        }
    }
}
