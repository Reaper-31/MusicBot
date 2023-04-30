using System.Diagnostics;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Audio;

public class AudioModule : ModuleBase<SocketCommandContext>
{
    [Command("play")]
    public async Task Play(string url)
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
            using (var ffmpeg = CreateFFmpegProcess(process.StandardOutput.BaseStream))
            {
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
                Arguments = "-i pipe:0 -ac 2 -f s16le -ar 48000 -vn pipe:1",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true
        };
    }
}
