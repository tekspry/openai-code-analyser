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

            var builder = new ConfigurationBuilder()
                           .SetBasePath(Directory.GetCurrentDirectory())
                           .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            AppConfig.Configuration = builder.Build();

            List<string> allFiles = await GetAllFiles(folderPath, (CodeOperation)Enum.Parse(typeof(CodeOperation), operation));

            await AnalyzeAllFiles(allFiles, (CodeOperation)Enum.Parse(typeof(CodeOperation), operation));

            Console.WriteLine("Operation executed!!");
        }

        static async Task<List<string>> GetAllFiles(string rootFolder, CodeOperation operation)
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
                        List<string> subFolderFiles = await GetAllFiles(directory, operation);
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
                    string directoryPath = Path.GetDirectoryName(filePath) + "\\" + codeAnalysisFilePath + "\\";

                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    var code = string.Empty;
                    code = File.ReadAllText(filePath);
                    Console.WriteLine();
                    Console.WriteLine(Path.GetFileName(filePath) + ": execution started...");

                    var outputPath = directoryPath + Path.GetFileNameWithoutExtension(filePath);

                    string fileExtension = Path.GetExtension(filePath);
                    
                    switch (operation)
                    {
                        case CodeOperation.gcaic:

                            await ExecuteAnalysis(codeAnalysisFileName, 
                            string.Concat(codeAnalysisPrompt.Replace("###", fileExtension, StringComparison.OrdinalIgnoreCase), $" \n {code} \n"), outputPath);
                            await ExecuteAnalysis(improvedCodeFileName, 
                            string.Concat(codegeneratePrompt.Replace("###", fileExtension, StringComparison.OrdinalIgnoreCase), $" \n {code} \n"), outputPath);

                            break;

                        case CodeOperation.gca:
                            await ExecuteAnalysis(codeAnalysisFileName, 
                            string.Concat(codeAnalysisPrompt.Replace("###", fileExtension, StringComparison.OrdinalIgnoreCase), $" \n {code} \n"), outputPath);

                            break;

                        case CodeOperation.gic:
                            await ExecuteAnalysis(improvedCodeFileName, 
                            string.Concat(codegeneratePrompt.Replace("###", fileExtension, StringComparison.OrdinalIgnoreCase), $" \n {code} \n"), outputPath);

                            break;
                        case CodeOperation.clearca:
                            if (Directory.Exists(directoryPath))
                                Directory.Delete(directoryPath, true);
                            
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
            //auth settings
            var apikey = AppConfig.Configuration.GetSection("OpenAIAuthSettings")["OpenApiKey"];

            //openai api completion parameters
            var maxTokens = Convert.ToInt32(AppConfig.Configuration.GetSection("OpenAICompletionsSettings")["maxTokens"]);
            var temperature = Convert.ToDouble(AppConfig.Configuration.GetSection("OpenAICompletionsSettings")["temperature"]);
            var presencePenalty = Convert.ToDouble(AppConfig.Configuration.GetSection("OpenAICompletionsSettings")["presencePenalty"]);
            var frequencyPenalty = Convert.ToDouble(AppConfig.Configuration.GetSection("OpenAICompletionsSettings")["frequencyPenalty"]);

            var api = new OpenAIClient(apikey);

            File.WriteAllText($"{outputPath}{fileName}", string.Empty);


            await api.CompletionsEndpoint.StreamCompletionAsync(result =>
            {
                foreach (var token in result.Completions)
                {
                    File.AppendAllText($"{outputPath}{fileName}", token.ToString());
                }
            }, prompt, maxTokens: maxTokens, temperature: temperature, presencePenalty: presencePenalty, frequencyPenalty: frequencyPenalty, model: Model.Davinci);
        }
    }
}