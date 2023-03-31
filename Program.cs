using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Models;

namespace codeanalyser.ai
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string operation = String.Empty;
            string folderPath = String.Empty;

            if (args.Length < 2)
            {   
                Console.WriteLine("pass the operation and folderpath as parameter");
                return;
            }
            else
            {
                operation = args[0];
                folderPath = args[1];
            }

             if (!Enum.TryParse(typeof(CodeOperation), operation, out var operationEnum))
            {
                Console.WriteLine("Invalid operation. Please provide a valid operation");
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine($"Folder path {folderPath} does not exist. Please provide a valid folder path");
                return;
            }

            var builder = new ConfigurationBuilder()
                           .SetBasePath(Directory.GetCurrentDirectory())
                           .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            AppConfig.Configuration = builder.Build();

            List<string> allFiles = await GetAllFiles(folderPath);

            await AnalyzeAllFiles(allFiles, (CodeOperation)operationEnum);

            Console.WriteLine("Operation executed!!");
        }

        public static async Task<List<string>> GetAllFiles(string rootFolder)
        {
            var allFiles = new List<string>();
                        
            try
            {   
                List<string> fileExclusionList = AppConfig.Configuration.GetSection("CodeAnalysisFileSettings:fileExclusionList").Get<List<string>>();
                var supportedFileExtensions = AppConfig.Configuration.GetSection("CodeAnalysisFileSettings:SupportedFileExtensions").Get<string[]>();
                
                string[] files = Directory.GetFiles(rootFolder, "*", SearchOption.AllDirectories);                

                foreach (string file in files)
                {
                    string fileExtension = Path.GetExtension(file);
                    if (!fileExclusionList.Any(p => file.Contains(p)) && Array.IndexOf(supportedFileExtensions, fileExtension)>= 0)
                    {
                        allFiles.Add(file);
                    }
                }

                foreach (string directory in Directory.GetDirectories(rootFolder))
                {
                    if (!directory.Contains("codeanalysis"))
                    {
                        List<string> subFolderFiles = await GetAllFiles(directory);
                        allFiles.AddRange(subFolderFiles);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return allFiles;
        }

        static async Task AnalyzeAllFiles(List<string> filePaths, CodeOperation operation)
        {
            try
            {
                // fetch configurations
                string codeAnalysisFilePath, codeAnalysisFileName, improvedCodeFileName, codeAnalysisPrompt, codegeneratePrompt;
                GetConfiguration(out codeAnalysisFilePath, out codeAnalysisFileName, out improvedCodeFileName, out codeAnalysisPrompt, out codegeneratePrompt);

                foreach (string filePath in filePaths)
                {  
                    string directoryPath = Path.Combine(Path.GetDirectoryName(filePath), codeAnalysisFilePath);

                    Directory.CreateDirectory(directoryPath);

                    var code = await File.ReadAllTextAsync(filePath);
                    Console.WriteLine();
                    Console.WriteLine($"{Path.GetFileName(filePath)}: execution started...");

                    var outputPath = Path.Combine(directoryPath, Path.GetFileNameWithoutExtension(filePath));

                    string fileExtension = Path.GetExtension(filePath);
                    
                    switch (operation)
                    {
                        case CodeOperation.gcaic:
                            await ExecuteAnalysis(codeAnalysisFileName,
                                string.Concat(codeAnalysisPrompt.Replace("###", Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase), $" \n {code} \n"), outputPath);
                            await ExecuteAnalysis(improvedCodeFileName,
                                string.Concat(codegeneratePrompt.Replace("###", Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase), $" \n {code} \n"), outputPath);
                            break;
                        case CodeOperation.gca:
                            await ExecuteAnalysis(codeAnalysisFileName,
                                string.Concat(codeAnalysisPrompt.Replace("###", Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase), $" \n {code} \n"), outputPath);
                            break;
                        case CodeOperation.gic:
                            await ExecuteAnalysis(improvedCodeFileName,
                                string.Concat(codegeneratePrompt.Replace("###", Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase), $" \n {code} \n"), outputPath);
                            break;
                        case CodeOperation.clearca:
                            if (Directory.Exists(directoryPath))
                            {
                                Directory.Delete(directoryPath, true);
                            }
                            break;
                        default:
                            Console.WriteLine("Unsupported operation");
                            break;
                    }

                    Console.WriteLine(Path.GetFileName(filePath) + ": execution complete...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void GetConfiguration(out string codeAnalysisFilePath, out string codeAnalysisFileName, out string improvedCodeFileName, out string codeAnalysisPrompt, out string codegeneratePrompt)
        {
            //file settings
            codeAnalysisFilePath = AppConfig.Configuration.GetSection("CodeAnalysisFileSettings")["CodeAnalysisFilePath"];
            codeAnalysisFileName = AppConfig.Configuration.GetSection("CodeAnalysisFileSettings")["CodeAnalysisFileName"];
            improvedCodeFileName = AppConfig.Configuration.GetSection("CodeAnalysisFileSettings")["ImprovedCodeFileName"];

            //prompts
            codeAnalysisPrompt = AppConfig.Configuration.GetSection("OpenAIPrompts")["CodeAnalysisPrompt"];
            codegeneratePrompt = AppConfig.Configuration.GetSection("OpenAIPrompts")["CodeGeneratePrompt"];
        }

        private static async Task ExecuteAnalysis(string fileName, string prompt, string outputPath)
        {
            try
            {
                var openAIAuthSettings = AppConfig.Configuration.GetSection("OpenAIAuthSettings");
                var openAICompletionsSettings = AppConfig.Configuration.GetSection("OpenAICompletionsSettings");

                // Authentication settings
                var apiKey = openAIAuthSettings["OpenAIKey"];

                // OpenAI API completion parameters
                var maxTokens = int.Parse(openAICompletionsSettings["maxTokens"]);
                var temperature = double.Parse(openAICompletionsSettings["temperature"]);
                var presencePenalty = double.Parse(openAICompletionsSettings["presencePenalty"]);
                var frequencyPenalty = double.Parse(openAICompletionsSettings["frequencyPenalty"]);

                // Instantiate OpenAIClient
                var openAIClient = new OpenAIClient(apiKey);

                // Create an empty file
                File.WriteAllText($"{outputPath}{fileName}", string.Empty);
                // Stream completion results and write to file
                await openAIClient.CompletionsEndpoint.StreamCompletionAsync(result =>
                {
                    foreach (var token in result.Completions)
                    {
                        File.AppendAllText($"{outputPath}{fileName}", token.ToString());
                    }
                }, prompt, maxTokens: maxTokens, temperature: temperature, presencePenalty: presencePenalty, frequencyPenalty: frequencyPenalty, model: Model.Davinci);
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Error executing analysis: {ex.Message}");
            }
        }

        private static bool TryParseOperation(string operation, out CodeOperation operationEnum)
        {
            return Enum.TryParse(operation, out operationEnum);
        }
    }
}