using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

class ArgumentParser
{
    private readonly Dictionary<string, string?> arguments;

    public List<string> PositionalArguments { get; }

    public ArgumentParser(string[] args)
    {
        arguments = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        PositionalArguments = new List<string>();

        ParseArguments(args);
    }

    private void ParseArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("-"))
            {
                string key = args[i].TrimStart('-');
                string? value = i < args.Length - 1 && !args[i + 1].StartsWith("-") ? args[i + 1] : null;
                arguments[key] = value;
                i++; // Increment i to skip the value
            }
            else
            {
                PositionalArguments.Add(args[i]);
            }
        }
    }

    public string? GetValue(string key)
    {
        arguments.TryGetValue(key.TrimStart('-'), out string? value);
        return value;
    }
}

class Program
{
    static void Main(string[] args)
    {
        var parser = new ArgumentParser(args);
        var filenames = parser.PositionalArguments;
        var writeToFile = parser.GetValue("-w");
        var apiToken = parser.GetValue("-a");

        // Verify at least one filename is specified, and if -w is used, an output file must be specified (and must not start with a "-")
        if ((filenames.Count == 0) || (args.Any(arg => arg == "-w" && (Array.IndexOf(args, "-w") + 1 >= args.Length))) || (args[Array.IndexOf(args, "-w") + 1].StartsWith("-")))
        {
            DisplayHelp();
            return;
        }

        var output = GetData(filenames, apiToken);

        if (!string.IsNullOrEmpty(writeToFile))
        {
            WriteData(output, writeToFile);
        }
        else
        {
            // Check to see if the list has any data other than the column header, and only print results if so
            if (output.Count > 1)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                if (output.Count == 2)
                {
                    Console.WriteLine($"\n\n{output.Count - 1} unique IP address found.");
                }
                else
                {
                    Console.WriteLine($"\n\n{output.Count - 1} unique IP addresses found.");
                }
                Console.ResetColor();

                PrintData(output);
            }
            else
            {
                PrintErrors("\n\nNo results found!");
            }
        }
    }

    static void DisplayHelp()
    {
        Console.WriteLine("Description:");
        Console.WriteLine("  SharpAbeebus version 1.0.0");
        Console.WriteLine("  Parses publicly routable IPv4 addresses from specified file(s) and obtains GeoIP information from IPinfo.io");
        Console.WriteLine("\nAuthor:");
        Console.WriteLine("  Richard Davis, 13Cubed Studios LLC (13cubed.com)");
        Console.WriteLine("\nExamples:");
        Console.WriteLine("  SharpAbeebus.exe file1.txt");
        Console.WriteLine("  SharpAbeebus.exe *.csv");
        Console.WriteLine("  SharpAbeebus.exe C:\\Users\\demo\\Desktop\\*");
        Console.WriteLine("  SharpAbeebus.exe file1.txt file2.txt -w out.csv");
        Console.WriteLine("  SharpAbeebus.exe file1.txt file2.txt -w C:\\Users\\demo\\Desktop\\out.csv -a TOKEN");
        Console.WriteLine("\nUsage:");
        Console.WriteLine("  SharpAbeebus.exe filename(s) [options]");
        Console.WriteLine("\nOptions:");
        Console.WriteLine("  -w <filename>   Write the results to the specified file (if omitted, results will be displayed in the console)");
        Console.WriteLine("  -a <apiToken>   Specify an IPinfo.io API token (NOT required)");
    }

    static void PrintErrors(string errorText)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.Write($"{errorText}");
        Console.ResetColor();
    }

    static List<string> GetData(List<string> filenames, string? apiToken)
    {
        var results = new List<string>();
        var addresses = new List<string>();
        var filteredAddresses = new List<string>();
        long filesize;

        Console.WriteLine("\nReading File:");

        foreach (var filename in filenames)
        {
            try
            {
                var expandedFilenames = ExpandWildcards(filename);
                foreach (var expandedFilename in expandedFilenames)
                {
                    try
                    {
                        filesize = new System.IO.FileInfo(expandedFilename).Length;
                        string formattedFilesize = filesize.ToString("N0");

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write($"\n{expandedFilename}");
                        Console.ResetColor();
                        Console.Write($" ({formattedFilesize} bytes)  ");

                        // Use a separate thread to read the file and display the rotating cursor
                        var fileReadingThread = new Thread(() =>
                        {
                            const int bufferSize = 16384;

                            using (var fileStream = new FileStream(expandedFilename, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize))
                            using (var reader = new StreamReader(fileStream))
                            {
                                char[] buffer = new char[bufferSize];
                                int bytesRead;

                                while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    string chunk = new string(buffer, 0, bytesRead);

                                    // Process the chunk (e.g., find IP addresses)
                                    addresses.AddRange(Regex.Matches(chunk, @"\b(?!(10\.|172\.(1[6-9]|2[0-9]|3[0-1])\.|192\.168\.|169\.254\.|127\.|0\.|2[2-9][4-9]|2[3-5][0-5]|2[0-1][0-9][0-9]))((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b")
                                        .Cast<Match>().Select(m => m.Value));
                                }
                            }
                        });

                        fileReadingThread.Start();

                        // Display rotating cursor while the file is being read
                        var cursorFrames = new[] { '|', '/', '-', '\\' };
                        int cursorFrameIndex = 0;

                        while (fileReadingThread.IsAlive)
                        {
                            Console.Write("\x1B[1D"); // Move the cursor back one character
                            Console.Write(cursorFrames[cursorFrameIndex]); // Display the rotating cursor
                            cursorFrameIndex = (cursorFrameIndex + 1) % cursorFrames.Length;
                            System.Threading.Thread.Sleep(100); // Adjust the sleep duration as needed
                        }

                        // Join the file reading thread to ensure it completes before moving on
                        fileReadingThread.Join();

                        // Remove the rotating cursor after the file has been read
                        Console.Write("\x1B[1D\x1B[1P");
                        Console.Write("\x1B[1D\x1B[1P");
                    }
                    catch
                    {
                        PrintErrors($"\n{filename} (error opening file)");
                        continue;
                    }
                }
            }
            catch (IOException)
            {
                PrintErrors($"{filename} (error opening file)");
                continue;
            }
        }

        var addressCounts = addresses.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        addresses = addresses.Distinct().ToList();

        var total = filteredAddresses.Count;
        var i = 0;

        foreach (var address in addresses)
        {
            filteredAddresses.Add(address);
        }

        total = filteredAddresses.Count;
        i = 0;

        // If any valid addresses are found, insert a new line to make room for the progress bar
        if (total > 0)
        {
            Console.Write("\n\nGetting Results:\n\n");
        }

        foreach (var filteredAddress in filteredAddresses)
        {
            ProgressBar(i + 1, total);
            i++;

            var formattedData = "";

            // Remove any leading zeros from each octet before passing to ipinfo.io
            var cleanedAddress = Regex.Replace(filteredAddress, @"\b(?:0*([1-9][0-9]*|0))\b", "$1");

            string url;
            if (!string.IsNullOrEmpty(apiToken))
            {
                url = $"https://ipinfo.io/{cleanedAddress}/json/?token={apiToken}";
            }
            else
            {
                url = $"https://ipinfo.io/{cleanedAddress}/json";
            }

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var rawData = httpClient.GetStringAsync(url).Result;
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(rawData);

                    if (data != null)
                    {
                        var keys = new[] { "ip", "hostname", "country", "region", "city", "postal", "loc", "org" };

                        foreach (var key in keys)
                        {
                            if (data.ContainsKey(key))
                            {
                                var value = data[key].ToString() ?? "N/A";

                                formattedData += key == "loc" ? $"{value}," : $"{value.Replace(",", "")},";
                            }
                            else
                            {
                                formattedData += key == "loc" ? "N/A,N/A," : "N/A,";
                            }
                        }

                        var addressCount = addressCounts[filteredAddress];
                        formattedData += addressCount;

                        results.Add(formattedData);
                    }
                    else
                    {
                        PrintErrors($"\nFailed to download data from {url}.");
                    }
                }
            }
            catch
            {
                if (!string.IsNullOrEmpty(apiToken))
                {
                    PrintErrors("\n\nCould not get results. Invalid API key?");
                    Environment.Exit(1);
                }

                PrintErrors($"\nError parsing address: {filteredAddress}");
                continue;
            }
        }

        // Sort addresses by count (descending)
        results = results.OrderByDescending(x =>
        {
            // Split the line into fields
            var fields = x.Split(',');

            // Try parsing the count field, defaulting to 0 if it's not a valid integer
            if (int.TryParse(fields.ElementAtOrDefault(9), out var count))
            {
                return count;
            }
            else
            {
                // Handle the case where the count is not a valid integer
                PrintErrors($"\nInvalid count value: {fields.ElementAtOrDefault(9)}");
                return 0; // Set a default value or handle it according to your requirements
            }
        }).ToList();

        results.Insert(0, "IP Address,Hostname,Country,Region,City,Postal Code,Latitude,Longitude,ASN,Count");

        return results;
    }

    static void PrintData(List<string> results)
    {
        var rowCount = 0;
        Console.Write("\n");

        if (results.Count == 0)
        {
            PrintErrors("\nNo data to print.");
            return;
        }

        var rows = results.Select(line => line.Split(','));
        var numColumns = rows.First().Length;

        if (rows.Any(row => row.Length != numColumns))
        {
            PrintErrors("\nInconsistent column count in the data.");
            return;
        }

        var widths = Enumerable.Range(0, numColumns)
            .Select(col => rows.Max(row => row[col].Length))
            .ToList();

        foreach (var row in rows)
        {
            if (row.Length == numColumns)
            {
                var formattedRow = string.Join(" | ", row.Select((cell, index) => cell.PadRight(widths[index])));
                if (rowCount == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan; // Set the color for the column header
                    Console.WriteLine(formattedRow);
                    Console.ResetColor(); // Reset the color after printing the column header
                }
                else
                {
                    Console.WriteLine(formattedRow);
                }
            }
            else
            {
                PrintErrors("\nInconsistent column count in a row: " + string.Join(", ", row));
            }
            rowCount++;
        }
    }

    static void WriteData(List<string> results, string? outfile)
    {
        if (string.IsNullOrEmpty(outfile))
        {
            PrintErrors("\nInvalid filename for -w option.");
            Environment.Exit(1);
        }

        try
        {
            // Write the results to the file
            File.WriteAllLines(outfile, results);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n\nResults written to: {outfile}");
            Console.ResetColor();
        }
        catch (IOException)
        {
            PrintErrors($"\n\nCould not write the specified file: {outfile}");
            Environment.Exit(1);
        }
    }

    static void ProgressBar(int count, int total)
    {
        var bar_len = 50;
        var filled_len = (int)Math.Round(bar_len * count / (double)total);
        var percents = Math.Round(100.0 * count / total, 1);
        var bar = new string('█', filled_len) + new string('.', bar_len - filled_len);

        if (percents == 100)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"\r[{bar}] {percents}% Done!");
            Console.ResetColor();
        }
        else
        {
            Console.Write($"\r[{bar}] {percents}%");
        }
    }

    static IEnumerable<string> ExpandWildcards(string path)
    {
        // If the path contains wildcards, manually expand them
        if (path.Contains("*") || path.Contains("?"))
        {
            string directory = Path.GetDirectoryName(path) ?? "."; // Set a default value if null
            string filename = Path.GetFileName(path) ?? "*.*";     // Set a default value if null

            // Ensure we have a valid directory path
            directory = string.IsNullOrEmpty(directory) ? "." : directory;

            // Enumerate files matching the pattern
            IEnumerable<string> files = Directory.GetFiles(directory, filename);

            // If no files match, return the original path
            if (!files.Any())
            {
                return new List<string> { path };
            }

            return files;
        }

        // If the path represents an existing directory, manually expand it
        if (Directory.Exists(path))
        {
            // Enumerate files in the directory (without subdirectories)
            IEnumerable<string> files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);

            // If no files match, return the original path
            if (!files.Any())
            {
                return new List<string> { path };
            }

            return files;
        }

        // If the path is neither a wildcard nor a directory, return it as is
        return new List<string> { path };
    }
}