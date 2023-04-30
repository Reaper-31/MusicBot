using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;

namespace YourBotName
{
    class Program
    {
        private DiscordSocketClient _client;
        private CommandService _commands;

        static void Main(string[] args)
        => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            _commands = new CommandService();
            await RegisterCommandsAsync();
            _client.Log += Log;
            string botToken = "bot token";
            await _client.LoginAsync(TokenType.Bot, botToken);
            await _client.StartAsync();
            await Task.Delay(-1);
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task RegisterCommandsAsync()
        {
            _client.MessageReceived += HandleCommandAsync;
            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(), services: null);
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            if (message == null) return;
            int argPos = 0;
            if (message.HasStringPrefix("=", ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos))
            {
                var context = new SocketCommandContext(_client, message);
                Console.WriteLine($"Received command: {message.Content}");
                var result = await _commands.ExecuteAsync(context: context, argPos: argPos, services: null);
                if (!result.IsSuccess)
                    Console.WriteLine(result.ErrorReason);
            }
        }
    }

    public class AudioModule : ModuleBase<SocketCommandContext>
    {
        [Command("play", RunMode = RunMode.Async)]
        public async Task PlayAsync([Remainder] string url)
        {
            await Play(url);
        }

        [Command("join", RunMode = RunMode.Async)]
        public async Task JoinAsync()
        {
            Console.WriteLine("JoinAsync called");
            await JoinVoiceChannel();
        }

        private async Task JoinVoiceChannel()
        {
            Console.WriteLine("JoinVoiceChannel called");
            var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
            if (voiceChannel == null)
            {
                await Context.Channel.SendMessageAsync("You must be in a voice channel to use this command.");
                return;
            }

            try
            {
                await voiceChannel.ConnectAsync();
                Console.WriteLine("Connected to voice channel");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to voice channel: {ex.Message}");
            }
        }

        private async Task Play(string url)
        {
            var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
            if (voiceChannel == null)
            {
                await Context.Channel.SendMessageAsync("You must be in a voice channel to use this command.");
                return;
            }

            var audioClient = await voiceChannel.ConnectAsync();

            using (var process = CreateYoutubeDLProcess(url))
            {
                process.Start();
                using (var ffmpeg = CreateFFmpegProcess(process.StandardOutput.BaseStream))
                {
                    ffmpeg.Start();
                    using (var output = ffmpeg.StandardOutput.BaseStream)
                    {
                        using (var discord = audioClient.CreatePCMStream(AudioApplication.Mixed, 128 * 1024))
                        {
                            try
                            {
                                await output.CopyToAsync(discord);
                            }
                            finally
                            {
                                await discord.FlushAsync();
                            }
                        }
                    }
                }
            }

            await audioClient.StopAsync();
        }

        private Process CreateYoutubeDLProcess(string url)
        {
            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "youtube-dl",
                    Arguments = $"-f bestaudio -o - {url}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
        }

        private Process CreateFFmpegProcess(Stream input)
        {
            return new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-i pipe:0 -ac 2 -f s16le -ar 48000 -acodec pcm_s16le pipe:1",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true
            };


        }
    }
}

