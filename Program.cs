using System.Diagnostics;
using System.IO;

namespace VideoMaker;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Parse command line arguments manually for simplicity
        var inputDir = "./images";
        var outputFile = "./output.mp4";
        var imageDuration = 5;
        var fadeDuration = 1.0;
        var enableKenBurns = true;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-i":
                case "--input":
                    inputDir = args[++i];
                    break;
                case "-o":
                case "--output":
                    outputFile = args[++i];
                    break;
                case "-d":
                case "--duration":
                    imageDuration = int.Parse(args[++i]);
                    break;
                case "-f":
                case "--fade":
                    fadeDuration = double.Parse(args[++i]);
                    break;
                case "--no-ken-burns":
                    enableKenBurns = false;
                    break;
                case "-h":
                case "--help":
                    Console.WriteLine("Video Maker - Convert JPEG images to MP4 video with crossfade transitions");
                    Console.WriteLine("Usage: VideoMaker [options]");
                    Console.WriteLine("Options:");
                    Console.WriteLine("  -i, --input <dir>     Input directory containing JPEG images (default: ./images)");
                    Console.WriteLine("  -o, --output <file>   Output video file (default: ./output.mp4)");
                    Console.WriteLine("  -d, --duration <sec>  Duration to show each image in seconds (default: 5)");
                    Console.WriteLine("  -f, --fade <sec>      Fade transition duration in seconds (default: 1.0)");
                    Console.WriteLine("  --no-ken-burns        Disable Ken Burns effect (enabled by default)");
                    Console.WriteLine("  -h, --help            Show this help message");
                    return 0;
            }
        }

        try
        {
            var videoMaker = new VideoMaker(new DirectoryInfo(inputDir), new FileInfo(outputFile), imageDuration, fadeDuration, enableKenBurns);
            await videoMaker.CreateVideoAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}

public class VideoMaker
{
    private readonly DirectoryInfo _inputDir;
    private readonly FileInfo _outputFile;
    private readonly int _imageDuration;
    private readonly double _fadeDuration;
    private readonly bool _enableKenBurns;
    private const int OutputWidth = 1920;
    private const int OutputHeight = 1080;
    private const int Fps = 25;

    public VideoMaker(DirectoryInfo inputDir, FileInfo outputFile, int imageDuration, double fadeDuration, bool enableKenBurns = true)
    {
        _inputDir = inputDir;
        _outputFile = outputFile;
        _imageDuration = imageDuration;
        _fadeDuration = fadeDuration;
        _enableKenBurns = enableKenBurns;
    }

    public async Task CreateVideoAsync()
    {
        try
        {
            Console.WriteLine("Starting video creation process...");
            
            var imageFiles = ValidateInput();
            Console.WriteLine($"Found {imageFiles.Length} JPEG files");

            if (imageFiles.Length == 0)
            {
                throw new InvalidOperationException("No JPEG files found in input directory");
            }

            if (imageFiles.Length == 1)
            {
                await CreateSimpleVideoAsync(imageFiles);
            }
            else if (_fadeDuration > 0)
            {
                await CreateCrossfadeVideoAsync(imageFiles);
            }
            else
            {
                await CreateSimpleVideoAsync(imageFiles);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }
    }

    private string[] ValidateInput()
    {
        if (!_inputDir.Exists)
        {
            throw new DirectoryNotFoundException($"Input directory {_inputDir.FullName} does not exist");
        }

        var imageFiles = _inputDir.GetFiles("*.jpg", SearchOption.TopDirectoryOnly)
            .Concat(_inputDir.GetFiles("*.jpeg", SearchOption.TopDirectoryOnly))
            .OrderBy(f => f.Name)
            .Select(f => f.FullName)
            .ToArray();

        return imageFiles;
    }

    private async Task CreateCrossfadeVideoAsync(string[] imageFiles)
    {
        Console.WriteLine($"Creating crossfade video with {imageFiles.Length} images using xfade filter");
        
        if (imageFiles.Length == 2)
        {
            await CreateTwoImageCrossfadeAsync(imageFiles);
        }
        else
        {
            await CreateMultiImageCrossfadeAsync(imageFiles);
        }
    }

    private async Task CreateTwoImageCrossfadeAsync(string[] imageFiles)
    {
        var img1 = imageFiles[0];
        var img2 = imageFiles[1];
        var fadeOffset = _imageDuration - _fadeDuration;
        var filterComplex = BuildTwoImageFilterComplex(fadeOffset);

        // For crossfade to work properly, we need to extend input duration for overlap
        var inputDuration = _imageDuration + _fadeDuration;
        
        var arguments = $"-loop 1 -t {inputDuration} -i \"{img1}\" -loop 1 -t {inputDuration} -i \"{img2}\" -filter_complex \"{filterComplex}\" -map [out] -c:v libx264 -crf 23 -r {Fps} -pix_fmt yuv420p -colorspace bt709 -color_primaries bt709 -color_trc bt709 -y \"{_outputFile.FullName}\"";

        await RunFFmpegAsync(arguments);
        Console.WriteLine($"Video created successfully: {_outputFile.FullName}");
    }

    private async Task CreateMultiImageCrossfadeAsync(string[] imageFiles)
    {
        // For multi-image crossfade, we need to extend the duration of each input
        // to allow proper overlap for the xfade transitions
        var inputArgs = new List<string>();
        
        // For multi-image crossfade, we need to extend the duration of each input
        // to allow proper overlap for the xfade transitions
        var inputDuration = _imageDuration + _fadeDuration;
        
        // Add all image inputs with calculated duration
        for (int i = 0; i < imageFiles.Length; i++)
        {
            inputArgs.Add($"-loop 1 -t {inputDuration} -i \"{imageFiles[i]}\"");
        }

        var filterComplex = BuildMultiImageFilterComplex(imageFiles.Length);
        
        var arguments = $"{string.Join(" ", inputArgs)} -filter_complex \"{filterComplex}\" -map [out] -c:v libx264 -crf 23 -r {Fps} -pix_fmt yuv420p -colorspace bt709 -color_primaries bt709 -color_trc bt709 -y \"{_outputFile.FullName}\"";

        await RunFFmpegAsync(arguments);
        Console.WriteLine($"Video created successfully: {_outputFile.FullName}");
    }

    private string BuildTwoImageFilterComplex(double fadeOffset)
    {
        var img1Filter = BuildImageFilter(0, 0);
        var img2Filter = BuildImageFilter(1, 1);
        
        return $"{img1Filter};{img2Filter};[v0][v1]xfade=transition=fade:duration={_fadeDuration}:offset={fadeOffset}[out]";
    }

    private string BuildMultiImageFilterComplex(int imageCount)
    {
        var filters = new List<string>();
        
        // Scale and pad all inputs with Ken Burns effect
        for (int i = 0; i < imageCount; i++)
        {
            filters.Add(BuildImageFilter(i, i));
        }
        
        // Chain xfade filters
        string currentInput = "v0";
        
        for (int i = 0; i < imageCount - 1; i++)
        {
            string nextInput = $"v{i + 1}";
            string outputLabel = i == imageCount - 2 ? "out" : $"x{i}";
            
            // For xfade, the offset is when the transition should start
            // First transition: start at imageDuration - fadeDuration
            // Subsequent transitions: start at the end of the previous segment minus fadeDuration
            double fadeOffset = _imageDuration - _fadeDuration;
            if (i > 0) {
                // After the first transition, we need to account for the shortened duration
                fadeOffset = (_imageDuration - _fadeDuration) + i * (_imageDuration - _fadeDuration);
            }
            
            filters.Add($"[{currentInput}][{nextInput}]xfade=transition=fade:duration={_fadeDuration}:offset={fadeOffset}[{outputLabel}]");
            
            currentInput = outputLabel;
        }
        
        return string.Join(";", filters);
    }
    
    private string BuildImageFilter(int inputIndex, int imageIndex)
    {
        var baseFilter = $"[{inputIndex}:v]scale={OutputWidth}:{OutputHeight}:force_original_aspect_ratio=decrease," +
                        $"pad={OutputWidth}:{OutputHeight}:(ow-iw)/2:(oh-ih)/2:black," +
                        $"colorspace=bt709:iall=bt601-6-625:fast=1";
        
        if (_enableKenBurns)
        {
            var kenBurnsFilter = BuildKenBurnsFilter(imageIndex);
            return $"{baseFilter},{kenBurnsFilter}[v{inputIndex}]";
        }
        else
        {
            return $"{baseFilter}[v{inputIndex}]";
        }
    }
    
    private string BuildKenBurnsFilter(int imageIndex)
    {
        // Generate Ken Burns parameters for this image
        var kenBurns = GetKenBurnsParameters(imageIndex);
        
        // Create zoompan filter for Ken Burns effect
        // Use exact frame count to prevent timing issues
        var durationFrames = _imageDuration * Fps;
        
        // Format zoom values to avoid locale issues
        var startZoom = kenBurns.StartZoom.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        var endZoom = kenBurns.EndZoom.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        var startX = kenBurns.StartX.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        var endX = kenBurns.EndX.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        var startY = kenBurns.StartY.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        var endY = kenBurns.EndY.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        
        return $"zoompan=z='{startZoom}+({endZoom}-{startZoom})*on/{durationFrames}':" +
               $"x='iw/2-(iw/zoom/2)+({endX}-{startX})*on/{durationFrames}*iw':" +
               $"y='ih/2-(ih/zoom/2)+({endY}-{startY})*on/{durationFrames}*ih':" +
               $"d=1";
    }
    
    private KenBurnsParameters GetKenBurnsParameters(int imageIndex)
    {
        // Create different Ken Burns effects for variety
        var random = new Random(imageIndex + 42); // Seed for consistency
        
        var effects = new[]
        {
            // Zoom in from left to right
            new KenBurnsParameters(1.0, 1.2, 0.0, 0.1, 0.0, 0.0),
            // Zoom in from right to left  
            new KenBurnsParameters(1.0, 1.2, 0.1, 0.0, 0.0, 0.0),
            // Zoom in from top to bottom
            new KenBurnsParameters(1.0, 1.2, 0.0, 0.0, 0.0, 0.1),
            // Zoom in from bottom to top
            new KenBurnsParameters(1.0, 1.2, 0.0, 0.0, 0.1, 0.0),
            // Zoom out from center
            new KenBurnsParameters(1.2, 1.0, 0.0, 0.0, 0.0, 0.0),
            // Diagonal zoom in (top-left to bottom-right)
            new KenBurnsParameters(1.0, 1.15, 0.0, 0.05, 0.0, 0.05),
            // Diagonal zoom in (top-right to bottom-left)
            new KenBurnsParameters(1.0, 1.15, 0.05, 0.0, 0.0, 0.05),
            // Subtle zoom with slight pan
            new KenBurnsParameters(1.0, 1.1, 0.0, 0.02, 0.0, 0.02)
        };
        
        return effects[imageIndex % effects.Length];
    }
    
    private record KenBurnsParameters(
        double StartZoom,
        double EndZoom,
        double StartX,
        double EndX,
        double StartY,
        double EndY
    );

    private async Task CreateSimpleVideoAsync(string[] imageFiles)
    {
        Console.WriteLine($"Creating simple video with {imageFiles.Length} images (no crossfade)");
        
        // Create a temporary concat file
        var tempConcatFile = Path.GetTempFileName();
        var concatContent = new List<string>();
        
        try
        {
            foreach (var imageFile in imageFiles)
            {
                concatContent.Add($"file '{imageFile}'");
                concatContent.Add($"duration {_imageDuration}");
            }
            
            // Add the last image again without duration for proper ending
            if (imageFiles.Length > 0)
            {
                concatContent.Add($"file '{imageFiles.Last()}'");
            }
            
            await File.WriteAllLinesAsync(tempConcatFile, concatContent);
            
            string videoFilter;
            if (_enableKenBurns)
            {
                // For Ken Burns with concat, we need to apply the effect to each segment individually
                // This is more complex, so we'll fall back to creating individual videos and concatenating
                Console.WriteLine("Ken Burns effect with multiple images without crossfade is not yet implemented.");
                Console.WriteLine("Using standard scaling instead.");
                videoFilter = $"scale={OutputWidth}:{OutputHeight}:force_original_aspect_ratio=decrease,pad={OutputWidth}:{OutputHeight}:(ow-iw)/2:(oh-ih)/2:black";
            }
            else
            {
                videoFilter = $"scale={OutputWidth}:{OutputHeight}:force_original_aspect_ratio=decrease,pad={OutputWidth}:{OutputHeight}:(ow-iw)/2:(oh-ih)/2:black";
            }
            
            var arguments = $"-f concat -safe 0 -i \"{tempConcatFile}\" -vf {videoFilter} -c:v libx264 -crf 23 -r {Fps} -pix_fmt yuv420p -colorspace bt709 -color_primaries bt709 -color_trc bt709 -y \"{_outputFile.FullName}\"";

            await RunFFmpegAsync(arguments);
            Console.WriteLine($"Video created successfully: {_outputFile.FullName}");
        }
        finally
        {
            if (File.Exists(tempConcatFile))
            {
                File.Delete(tempConcatFile);
            }
        }
    }
    
    private async Task RunFFmpegAsync(string arguments)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processInfo };
        
        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();
        
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                if (e.Data.Contains("time="))
                {
                    Console.WriteLine($"Processing: {e.Data}");
                }
            }
        };
        
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                if (e.Data.Contains("time="))
                {
                    Console.WriteLine($"Processing: {e.Data}");
                }
            }
        };
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        await process.WaitForExitAsync();
        
        if (process.ExitCode != 0)
        {
            var error = errorBuilder.ToString();
            throw new InvalidOperationException($"FFmpeg failed with exit code {process.ExitCode}: {error}");
        }
    }
}
