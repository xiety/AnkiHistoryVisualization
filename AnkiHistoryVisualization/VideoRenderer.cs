using System.Drawing;
using System.Drawing.Imaging;

using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;

namespace AnkiHistoryVisualization;

public static class VideoRenderer
{
    public static void ToVideo(string videoFile, IEnumerable<Bitmap> images)
    {
        var settings = FFMpegArguments
                    .FromPipeInput(new ImageSequencePipeSource(images))
                    .OutputToFile(videoFile, true, options => options
                        .WithVideoCodec(VideoCodec.LibX264)
                        .ForcePixelFormat("yuv420p")
                        .WithConstantRateFactor(14)
                        .WithFastStart());

        settings.ProcessSynchronously();
    }

    public static void ToImages(string folder, IEnumerable<Bitmap> images)
    {
        var imageIndex = 0;

        foreach (var image in images)
        {
            image.Save(Path.Combine(folder, $"{imageIndex:00000}.png"), ImageFormat.Png);
            imageIndex++;
        }
    }
}

file sealed class ImageSequencePipeSource(IEnumerable<Bitmap> images) : IPipeSource
{
    public string GetStreamArguments()
        => "-f image2pipe";

    public async Task WriteAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        foreach (var image in images)
        {
            image.Save(outputStream, ImageFormat.Bmp);
            await outputStream.FlushAsync(cancellationToken);
        }
    }
}
