using System.Drawing;
using System.Drawing.Imaging;

using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;

using FFMpegCore.Extensions.System.Drawing.Common;

namespace AnkiHistoryVisualization;

public static class VideoRenderer
{
    public static void ToVideo(string videoFile, float fps, IEnumerable<Bitmap> images)
    {
        var videoFramesSource = new RawVideoPipeSource(
            images.Select(bitmap => new BitmapVideoFrameWrapper(bitmap)))
        {
            FrameRate = fps
        };

        var settings = FFMpegArguments
                    .FromPipeInput(videoFramesSource)
                    .OutputToFile(videoFile, true, options => options
                        .WithVideoCodec(VideoCodec.LibX265)
                        .ForcePixelFormat("yuv420p")
                        .WithConstantRateFactor(24)
                        .WithFramerate(fps)
                        .WithFastStart());

        settings.ProcessSynchronously();
    }

    public static void ToImages(string folder, IEnumerable<Bitmap> images)
    {
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        var imageIndex = 0;

        foreach (var image in images)
        {
            image.Save(Path.Combine(folder, $"{imageIndex:00000}.png"), ImageFormat.Png);
            imageIndex++;
        }
    }
}
