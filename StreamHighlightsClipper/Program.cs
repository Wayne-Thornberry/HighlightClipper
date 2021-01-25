using System;
using System.Diagnostics; 
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using MediaToolkit;
using MediaToolkit.Model; 

namespace StreamHighlightsClipper
{
    internal class Program
    {
        private static readonly int BUFFER = 5;
        private static string _sourceFolder = "";
        private static string _resultFolder = "";
        private static string _mffpegFolderLoc = "";
        private static string _streamHighlightData = "";

        public static void Main(string[] args)
        {
            LoadConfig();
            Files = GetFiles(_sourceFolder);
            var index = GetIndexSelection("Please select an option:\n" +
                                          "0: Create highlights of all vods in folder\n" +
                                          "1: Select a specific vod to create highlights from\n"
                + "2: Select a specific vod and timestamp to create a highlight from\n", 3);
                        
            switch (index)
            {
                case 0:
                    for (int i = 0; i < Files.Length; i++)
                    {
                        Console.WriteLine($"[{i}] {GetFileName(Files[i])}"); 
                        DoHighlights(GetFileName(Files[i]), Files[i]);
                    } 
                    break;
                case 1:
                    for (int i = 0; i < Files.Length; i++)
                    {
                        Console.WriteLine($"[{i}] {GetFileName(Files[i])}"); 
                    }
                    index = GetIndexSelection("Please select an index: \n", Files.Length);
                    DoHighlights(GetFileName(Files[index]), Files[index]);
                    break;
                case 2:
                    
                    break;
            }  
        } 

        private static string GetFileName(string file)
        {
            file = Regex.Replace(file.Split('\\').Last(), @"\.[^.]+$", "");
            return file;
        }

        public static string[] Files { get; set; }

        private static int GetIndexSelection(string text, int max)
        {
            Console.WriteLine(text);
            do
            {
                var input = Console.ReadLine();
                if (!string.IsNullOrEmpty(input))
                {
                    if (int.TryParse(input, out var i))
                    {
                        if (i < max)
                        {
                            Console.WriteLine("\n");
                            return i;
                        }
                        Console.WriteLine("Invalid index selected, please select a valid index");
                    }
                    else
                    {
                        Console.WriteLine("Failed to detect an index");
                    }
                }
                else
                {
                    Console.WriteLine("Please input an index... ");
                }
            } while (true);
        }

        private static string[] GetFiles(string sourceFolder)
        {
            var files = Directory.GetFiles(sourceFolder);
            return files;
        }

        private static void LoadConfig()
        {
            var config = File.ReadLines("config.txt").ToArray();
            _streamHighlightData = File.ReadAllText("stream_highlights.txt");
            _sourceFolder = Directory.GetCurrentDirectory() + @"\" + config[0];
            _resultFolder = Directory.GetCurrentDirectory() + @"\" + config[1];
            _mffpegFolderLoc = Directory.GetCurrentDirectory() + @"\" + config[2];
            Console.WriteLine(_mffpegFolderLoc);
        }


        private static void DoHighlights(string videoName, string dir)
        {
            Console.WriteLine("Processing " + videoName );
            var file = new MediaFile(dir);
            using (var engine = new Engine())
            {
                engine.GetMetadata(file);
            }
            var split = videoName.Split('_'); 
            var date = split[0];
            var highlightMatches = Regex.Matches(_streamHighlightData, @"(("+date+@") \d{2}:\d{2}:\d{2}).*\](.*?)\(.*");
            if (highlightMatches.Count == 0) return;
            var buffer = new TimeSpan(0, 0, BUFFER, 0);
            var folderName = "Highlights-" + videoName;
            var resultFolder = $"{_resultFolder}{folderName}";
            var videoEndDateTime = DateTime.ParseExact(videoName, "yyyy-MM-dd_HH-mm-ss", null);
            var videoStartDateTime = videoEndDateTime.Subtract(file.Metadata.Duration);
            var videoFormat = ".mp4";
            var videoSource = $"{_sourceFolder}{videoName}{videoFormat}";
            
            Directory.CreateDirectory(resultFolder);
            for (var i = 0; i < highlightMatches.Count; i++)
            {
                var highlightMatch = highlightMatches[i]; 
                var highlightTimestamp = DateTime.Parse(highlightMatch.Groups[1].ToString());
                if ((highlightTimestamp <= videoStartDateTime) || (highlightTimestamp >= videoEndDateTime)) continue;
                var highlightTitle = string.IsNullOrEmpty(highlightMatch.Groups[3].ToString()) ? "NoTitle" : highlightMatch.Groups[3].ToString();
                highlightTitle = Regex.Replace(highlightTitle, " ", "_");
                var highlightName = highlightTimestamp.ToString("yyyy-dd-MM_HH-mm-ss") + highlightTitle + i;
                var highlightTimeSpan = highlightTimestamp.Subtract(videoStartDateTime);
                var highlightStart = highlightTimeSpan.Subtract(buffer).ToString();
                var highlightEnd = highlightTimeSpan.Add(buffer).ToString();
                var videoOutput = $@"{resultFolder}\{highlightName}{videoFormat}";
                var arguments = $"-n -ss {highlightStart} -to {highlightEnd} -i {videoSource} -map 0 -c copy -start_at_zero -avoid_negative_ts 1 -async 1 {videoOutput} ";
                try
                {                
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = _mffpegFolderLoc, 
                        Arguments = arguments, 
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    var process = Process.Start(startInfo);
                    Console.WriteLine(arguments);
                    Console.WriteLine(DateTime.Now.ToString("hh:mm:ss") + " Processing video... please wait");
                    while (!process.HasExited)
                    {

                    }
                    Console.WriteLine(DateTime.Now.ToString("hh:mm:ss") + " Video Processed");
                    if (process.ExitCode != 0)
                    {
                        Console.WriteLine("A highlight has failed to be created, check the arguments and try again...");
                        return;
                    } 
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }

            Console.WriteLine("All highlights created");
            Thread.Sleep(1000);
        }
    }
}
