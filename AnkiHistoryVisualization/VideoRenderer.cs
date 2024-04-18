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
                        .WithConstantRateFactor(15)
                        .WithFastStart());

        settings.ProcessSynchronously();
    }

    public static void ToImages(string folder, IEnumerable<Bitmap> images)
    {
        var image_index = 0;

        foreach (var image in images)
        {
            image.Save(Path.Combine(folder, $"{image_index:00000}.png"), ImageFormat.Png);
            image_index++;
        }
    }
}

file class ImageSequencePipeSource(IEnumerable<Bitmap> images) : IPipeSource
{
    public string GetStreamArguments()
        => "-f image2pipe";

    public async Task WriteAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        foreach (var image in images)
        {
            image.Save(outputStream, ImageFormat.Png);
            await outputStream.FlushAsync(cancellationToken);
        }
    }
}
