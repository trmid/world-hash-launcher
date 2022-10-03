using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text;

namespace WorldHash
{
    class Program
    {

        static string Branch = "release";
        static string AppDir = $"world-hash-{Branch}";
        static string ConfigFilename = $"{AppDir}\\config.json";

        static void Log(string msg)
        {
            DateTime now = DateTime.Now;
            Console.WriteLine($"[{now.ToShortTimeString()}] {msg}");
        }

        static string GetResponse(string prompt, string? defaultResponse)
        {
            Console.Write($"{prompt} " + ((defaultResponse == null) ? "" : $"({defaultResponse}) "));
            string response = (Console.ReadLine() ?? "").Trim();
            if (response.Length == 0) return defaultResponse ?? "";
            else return response;
        }

        static void InstallLatest()
        {
            string zipFilename = "world-hash.zip";
            Log("Could not find package. Beginning Installation...");
            Log("Cleaning directory...");
            if (File.Exists(zipFilename))
            {
                Log("Removing previous package...");
                File.Delete(zipFilename);
            }
            if (Directory.Exists(AppDir))
            {
                Log("Cleaning current installation...");
                string? configStr = null;
                if (File.Exists(ConfigFilename))
                {
                    Log("Backing up configuration...");
                    configStr = File.ReadAllText(ConfigFilename);
                }
                Log("Removing old installation...");
                Directory.Delete(AppDir, true);
                if(configStr != null)
                {
                    Log("Restoring configuration...");
                    File.WriteAllText(ConfigFilename, configStr);
                }
            }
            Log("Downloading package. This may take a minute...");
            var client = new WebClient();
            client.DownloadFile($"https://github.com/trmid/world-hash/archive/refs/heads/{Branch}.zip", zipFilename);
            Log("Unzipping package...");
            ZipFile.ExtractToDirectory(zipFilename, "./");
            Log("Cleaning directory...");
            File.Delete(zipFilename);
            Log("Installing dependencies...");
            RunCmd("cmd", "/c npm i", AppDir);
            RunCmd("cmd", "/c npm run build", AppDir);
            Log("Installation complete!");
        }

        static void Main(string[] args)
        {

            // Set console title:
            Console.Title = "World Hash";

            // Log banner:
            Console.WriteLine(@"
 ##      ##  #######  ########  ##       ########     ##     ##    ###     ######  ##     ## 
 ##  ##  ## ##     ## ##     ## ##       ##     ##    ##     ##   ## ##   ##    ## ##     ## 
 ##  ##  ## ##     ## ##     ## ##       ##     ##    ##     ##  ##   ##  ##       ##     ## 
 ##  ##  ## ##     ## ########  ##       ##     ##    ######### ##     ##  ######  ######### 
 ##  ##  ## ##     ## ##   ##   ##       ##     ##    ##     ## #########       ## ##     ## 
 ##  ##  ## ##     ## ##    ##  ##       ##     ##    ##     ## ##     ## ##    ## ##     ## 
  ###  ###   #######  ##     ## ######## ########     ##     ## ##     ##  ######  ##     ## 
            ");

            string nodeVersion = RunCmd("node", "--version", null, true).Trim();
            string npmVersion = RunCmd("cmd", "/c npm --version", null, true).Trim();

            // Check node and npm version:
            Match nodeVerCheck = (new Regex(@"^v(\d+)([\d\.]+)")).Match(nodeVersion);
            Match npmVerCheck = (new Regex(@"^([\d\.]+)")).Match(npmVersion);
            if(!(nodeVerCheck.Success && npmVerCheck.Success))
            {
                RunCmd("cmd", "/c Start https://nodejs.org/en/download/");
                throw new Exception("Node.js is not installed or is not configured correctly. Please install Node.js before continuing.");
            }

            // Check if node version is compatable:
            int bigVersion = int.Parse(nodeVerCheck.Groups[1].Value);
            if(bigVersion < 16)
            {
                RunCmd("cmd", "/c Start https://nodejs.org/en/download/");
                throw new Exception("Node.js installation is out of date. Please update to v16 or greater before continuing.");
            }

            Log("All dependencies checked! node: " + nodeVerCheck.Groups[1].Value + nodeVerCheck.Groups[2].Value + ", npm: " + npmVerCheck.Groups[1].Value);

            // Load package.json and check for updates:
            if (Directory.Exists(AppDir))
            {
                PackageJson localJson = PackageJson.ReadFromPath(Path.Combine(AppDir, "package.json"));
                try
                {
                    Log("Checking for updates...");
                    PackageJson remoteJson = PackageJson.ReadFromURL($"https://raw.githubusercontent.com/trmid/world-hash/{Branch}/package.json");
                    if(localJson.Version.CompareTo(remoteJson.Version) != 0)
                    {
                        Log($"An update is available for World Hash ({remoteJson.Version}). Would you like to install it? (Y/N) ");
                        while (true)
                        {
                            string input = Console.ReadLine() ?? "";
                            if ((new Regex(@"^y|Y")).Match(input).Success)
                            {
                                InstallLatest();
                                break;
                            } else if((new Regex(@"^n|N")).Match(input).Success)
                            {
                                Log("Installation bypassed.");
                                break;
                            }
                            Log("Please enter Y for yes, or N for no: ");
                        }
                    } else
                    {
                        Log("Up to date!");
                    }
                } catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                    Log("Could not fetch remote package to check for updates...");
                }
            } else
            {
                InstallLatest();
            }

            // Check for config.json
            Log("Loading configuration...");
            if (!File.Exists(ConfigFilename))
            {
                Log("Configuration not found.");
                Console.WriteLine("\n######################################################################");
                Console.WriteLine("Please provide the following configuration options:\nPress ENTER to accept the default in brackets.\n");
                Config config = new Config();
                config.IPFS_API = GetResponse("IPFS API URL:", "http://localhost:5001/");
                config.IPFS_GATEWAY = GetResponse("IPFS Gateway URL:", "http://localhost:8080/");
                config.ETHEREUM_RPC_URL = GetResponse("Ethereum RPC URL:", "https://cloudflare-eth.com/");
                config.MINECRAFT_SAVES_DIR = GetResponse("Minecraft Saves Directory:", Path.Combine(System.Environment.GetEnvironmentVariable("appdata"), ".minecraft\\saves"));
                config.MINECRAFT_SHORTCUT = GetResponse("Minecraft Shortcut:", "optional");
                if (config.MINECRAFT_SHORTCUT.Equals("optional")) config.MINECRAFT_SHORTCUT = null;
                Console.WriteLine("######################################################################\n");

                // Save file:
                Log("Saving configuration...");
                var options = new JsonSerializerOptions{
                    WriteIndented = true
                };
                File.WriteAllText(ConfigFilename, JsonSerializer.Serialize(config, options));
            }
            Log("Configuration loaded.");

            // Run App:
            PackageJson activePackage = PackageJson.ReadFromPath(Path.Combine(AppDir, "package.json"));
            Log($"Starting local web app ({activePackage.Version}) ...");
            RunCmd("cmd", "/c Start http://localhost:25557/");
            RunCmd("node", "build/index.js", AppDir, false, true, true, new string[][] { new string[] { "HOST", "localhost" }, new string[] { "PORT", "25557" } });
        }

        static bool OutLog = false;
        static bool ErrorLog = false;
        static StringBuilder StdOutput = new StringBuilder();

        /// <summary>
        /// Runs a command in a spawned process.
        /// </summary>
        /// <param name="cmd">The command to run</param>
        /// <param name="redirectOutput">If true, output will be returned as a string. Otherwise, output is directed to console.</param>
        /// <returns>string or null</returns>
        static string RunCmd(string cmd, string args, string? workingDir = null, bool returnOutput = false, bool logErrors = true, bool logOutput = false, string[][]? env = null)
        {
            System.Diagnostics.Process proc = new System.Diagnostics.Process
            {
                EnableRaisingEvents = false,
                StartInfo = {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    FileName = cmd,
                    Arguments = args,
                    CreateNoWindow = false
                }
            };
            if(workingDir != null)
            {
                proc.StartInfo.WorkingDirectory = workingDir;
            }
            if(env != null)
            {
                foreach (string[] pair in env)
                {
                    proc.StartInfo.Environment[pair[0]] = pair[1];
                }
            }
            OutLog = logOutput;
            ErrorLog = logErrors;
            StdOutput = new StringBuilder();
            proc.OutputDataReceived += new DataReceivedEventHandler(HandleStandardOutput);
            proc.ErrorDataReceived += new DataReceivedEventHandler(HandleStandardError);
            proc.Start();
            Console.Title = "World Hash";
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();
            proc.WaitForExit();
            if (returnOutput) {
                return StdOutput.ToString();
            } else {
                return "";
            }
        }

        static void HandleStandardOutput(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Console.Title = "World Hash";
            // Collect the net view command output.
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                // Add the text to the collected output.
                StdOutput.Append(outLine.Data + Environment.NewLine);

                // Log output if needed:
                if(OutLog)
                {
                    Console.WriteLine(outLine.Data);
                }
            }
        }

        static void HandleStandardError(object sendingProcess, DataReceivedEventArgs errLine)
        {
            // Collect the net view command output.
            if (!String.IsNullOrEmpty(errLine.Data))
            {
                // Log error if needed:
                if (ErrorLog)
                {
                    Console.WriteLine(errLine.Data);
                }
            }
        }
    }

    class PackageJson
    {
        public string Version { get; set; }
        public static PackageJson ReadFromPath(string path)
        {
            string json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            PackageJson packageJson = JsonSerializer.Deserialize<PackageJson>(json, options)!;
            return packageJson;
        }
        public static PackageJson ReadFromURL(string url)
        {
            var webRequest = (HttpWebRequest)HttpWebRequest.Create(url);
            webRequest.UserAgent = "world-hash-installer";
            var response = webRequest.GetResponse();
            var content = response.GetResponseStream();
            var reader = new StreamReader(content);
            string strContent = reader.ReadToEnd();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            PackageJson packageJson = JsonSerializer.Deserialize<PackageJson>(strContent, options)!;
            return packageJson;
        }
    }

    class Config
    {
        public string IPFS_API { get; set; }
        public string IPFS_GATEWAY { get; set; }
        public string ETHEREUM_RPC_URL { get; set; }
        public string MINECRAFT_SAVES_DIR { get; set; }
        public string? MINECRAFT_SHORTCUT { get; set; }
    }
}
