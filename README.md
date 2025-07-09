# VideoMaker - .NET Console Application

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Platform](https://img.shields.io/badge/platform-cross--platform-lightgrey.svg)](https://dotnet.microsoft.com/download)

A powerful .NET console application that converts a series of JPEG images into an MP4 video file with smooth crossfade transitions using FFmpeg's `xfade` filter.

## Features

- **Image Duration**: Each image is displayed for 5 seconds by default (configurable)
- **Crossfade Transitions**: 1-second crossfade between images by default (configurable)
- **Output Resolution**: 1920x1080 pixels (Full HD)
- **Aspect Ratio Preservation**: Images are scaled to fit with letterboxing if needed
- **Multiple Images Support**: Can handle 2 or more images with smooth xfade transitions

## Prerequisites

- .NET 9.0 or later
- FFmpeg installed and available in PATH
- JPEG images in the input directory

## Installation

1. Clone or download this project
2. Navigate to the VideoMaker directory
3. Build the project:
   ```bash
   dotnet build
   ```

## Usage

### Basic Usage

```bash
# Convert images in ./images directory to ./output.mp4
dotnet run

# Or after building:
./bin/Debug/net9.0/VideoMaker
```

### Command Line Options

```bash
dotnet run -- [options]
```

**Options:**
- `-i, --input <dir>` - Input directory containing JPEG images (default: ./images)
- `-o, --output <file>` - Output video file (default: ./output.mp4)
- `-d, --duration <sec>` - Duration to show each image in seconds (default: 5)
- `-f, --fade <sec>` - Fade transition duration in seconds (default: 1.0)
- `-h, --help` - Show help message

### Examples

```bash
# Use custom input directory and output file
dotnet run -- -i ./my-photos -o ./my-video.mp4

# Set each image to display for 3 seconds with 0.5 second crossfade
dotnet run -- -d 3 -f 0.5

# Create video with no crossfade (set fade to 0)
dotnet run -- -f 0

# Complete example with all options
dotnet run -- -i ./vacation-photos -o ./vacation-video.mp4 -d 4 -f 1.5
```

## How It Works

1. **Image Processing**: The application scans the input directory for JPEG files (*.jpg, *.jpeg)
2. **Crossfade Logic**: 
   - For 2 images: Uses direct xfade filter
   - For 3+ images: Chains multiple xfade filters for seamless transitions
3. **Video Generation**: Uses FFmpeg with the following settings:
   - Codec: H.264 (libx264)
   - Quality: CRF 23 (good balance of quality and file size)
   - Frame Rate: 25 fps
   - Pixel Format: yuv420p (widely compatible)

## Technical Details

- **Scaling**: Images are scaled to fit 1920x1080 while preserving aspect ratio
- **Padding**: Black bars are added if needed to maintain the target resolution
- **Crossfade**: Uses FFmpeg's xfade filter with fade transition
- **Timing**: Each crossfade starts before the current image ends for smooth transitions

## Troubleshooting

1. **FFmpeg not found**: Ensure FFmpeg is installed and available in your PATH
2. **No images found**: Check that the input directory contains JPEG files
3. **Build errors**: Ensure you have .NET 9.0 SDK installed

## Output

The application creates an MP4 video file with:
- Resolution: 1920x1080 (Full HD)
- Smooth crossfade transitions between images
- Professional quality encoding suitable for sharing or presentation

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [.NET 9.0](https://dotnet.microsoft.com/)
- Uses [FFmpeg](https://ffmpeg.org/) for video processing
- Crossfade transitions powered by FFmpeg's `xfade` filter
