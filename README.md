# openai-code-analyser
This project is a utility to generate code analysis for any project using OpenAI GPT 3

## Purpose
The purpose of this utility is to showcase how IT teams can leverage Generative AI to enhance their overall productivity. With this approach, Platform Engineering teams can create various tools to improve engineers' efficiency and speed up their delivery times.

#Installation
To install this application, follow these steps:

1. Clone the repository to local machine.
2. Open appsettings.json file.
3. Update the OpenAPIkey in it with the one you have generated.
4. Open the command prompt.
5. Build the project by running `dotnet publish -c release` command.
6. Next open the target project which need to be analyzed.
7. Add `codeanalysis/` to .gitignore file so that analyzed text files are checked in to git.
8. In command prompot go to the publish folder on this utility inside \bin\release\net6.0\publish
9. Execute command `.\codeanalyser.ai <operation> <folderpath>`
10. Utility will start analyzing all the files.

##Usage

Once the files are analyzed, go to codeanalysis folder and we can get review comments and improved code.
Currently this utility support 4 operations

1. gca - generate code analysis report
2. gic - generate improved version of analyzed code
3. gcaic - generate code analysis reprot along with improved code
4. clearca - to clear all code analysis files

## License

This project is released under the MIT License.```
