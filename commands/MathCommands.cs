using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System.Threading.Tasks;
using System;

namespace Frostbot.commands
{
    internal class MathCommands : BaseCommandModule
    {
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

        [Command("pow")]
        public async Task Power(CommandContext ctx, double x, double y)
        {
            double res = Math.Pow(x, y);
            await ctx.Channel.SendMessageAsync(res.ToString());
        }

        [Command("abs")]
        public async Task Abs(CommandContext ctx, double x)
        {
            double res = Math.Abs(x);
            await ctx.Channel.SendMessageAsync(res.ToString());
        }
    }
}
