using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.MSIdentity.Shared;
using Microsoft.DotNet.Scaffolding.Shared.CodeModifier.CodeChange;

namespace Microsoft.DotNet.Scaffolding.Shared.Project
{
    internal static class ProjectModifierHelper
    {
        internal static string[] CodeSnippetTrimStrings = new string[] { " ", "\r", "\n", ";" };
        internal static char[] CodeSnippetTrimChars = new char[] { ' ', '\r', '\n', ';' };
        internal const string VarIdentifier = "var";
        internal const string WebApplicationBuilderIdentifier = "WebApplicationBuilder";
        /// <summary>
        /// Check if Startup.cs or similar file exists.
        /// </summary>
        /// <returns>true if Startup.cs does not exist, false if it does exist.</returns>
        internal static async Task<bool> IsMinimalApp(IModelTypesLocator modelTypesLocator)
        {
            //find Startup if named Startup.
            var startupType = modelTypesLocator.GetType("Startup").FirstOrDefault();
            if (startupType == null)
            {
                //if changed the name in Program.cs, get the class name and check.
                var programDocument = modelTypesLocator.GetAllDocuments().Where(d => d.Name.EndsWith("Program.cs")).FirstOrDefault();
                var startupClassName = await GetStartupClassName(programDocument);
                startupType = modelTypesLocator.GetType(startupClassName).FirstOrDefault();
            }
            return startupType == null;
        }

        internal static async Task<bool> IsMinimalApp(CodeAnalysis.Project project)
        {
            if (project != null)
            {
                var startupDocument = project.Documents.Where(d => d.Name.EndsWith("Startup.cs")).FirstOrDefault();
                if (startupDocument == null)
                {
                    //if changed the name in Program.cs, get the class name and check.
                    var programDocument = project.Documents.Where(d => d.Name.EndsWith("Program.cs")).FirstOrDefault();
                    var startupClassName = await GetStartupClassName(programDocument);
                    if (!string.IsNullOrEmpty(startupClassName))
                    {
                        startupDocument = project.Documents.Where(d => d.Name.EndsWith($"{startupClassName}.cs")).FirstOrDefault();
                    }
                   
                }
                return startupDocument == null;
            }
            return false;
        }

        //Get Startup class name from CreateHostBuilder in Program.cs. If Program.cs is not being used, method
        //will bail out.
        internal static async Task<string> GetStartupClass(string projectPath, CodeAnalysis.Project project)
        {
            var programFilePath = Directory.EnumerateFiles(projectPath, "Program.cs").FirstOrDefault();
            if (!string.IsNullOrEmpty(programFilePath))
            {
                var programDoc = project.Documents.Where(d => d.Name.Equals(programFilePath)).FirstOrDefault();
                var startupClassName = await GetStartupClassName(programDoc);
                string className = startupClassName;
                var startupFilePath = string.Empty;
                if (!string.IsNullOrEmpty(startupClassName))
                {
                    return string.Concat(startupClassName, ".cs");
                }
            }
            return string.Empty;
        }

        internal static async Task<string> GetStartupClassName(Document programDoc)
        {
            if (programDoc != null && await programDoc.GetSyntaxRootAsync() is CompilationUnitSyntax root)
            {
                var namespaceNode = root.Members.OfType<NamespaceDeclarationSyntax>()?.FirstOrDefault();
                var programClassNode = namespaceNode?.DescendantNodes()
                    .Where(node =>
                        node is ClassDeclarationSyntax cds &&
                        cds.Identifier
                           .ValueText.Contains("Program"))
                    .First();

                var nodes = programClassNode?.DescendantNodes();
                var useStartupNode = programClassNode?.DescendantNodes()
                    .Where(node =>
                        node is MemberAccessExpressionSyntax maes &&
                        maes.ToString()
                            .Contains("webBuilder.UseStartup"))
                    .First();

                var useStartupTxt = useStartupNode?.ToString();
                if (!string.IsNullOrEmpty(useStartupTxt))
                {
                    int startIndex = useStartupTxt.IndexOf("<");
                    int endIndex = useStartupTxt.IndexOf(">");
                    if (startIndex > -1 && endIndex > startIndex)
                    {
                        return useStartupTxt.Substring(startIndex + 1, endIndex - startIndex - 1);
                    }
                }
            }
            return string.Empty;
        }

        internal static string GetClassName(string className)
        {
            string formattedClassName = string.Empty;
            if (!string.IsNullOrEmpty(className))
            {
                string[] blocks = className.Split(".cs");
                if (blocks.Length > 1)
                {
                    return blocks[0];
                }
            }
            return formattedClassName;
        }

        /// <summary>
        /// Format a string of a SimpleMemberAccessExpression(eg., Type.Value)
        /// Replace Type with its value from the parameterDict.
        /// </summary>
        /// <param name="codeBlock">SimpleMemberAccessExpression string</param>
        /// <param name="parameterDict">IDictionary with parameter type keys and values</param>
        /// <returns></returns>
        internal static string FormatCodeBlock(string codeBlock, IDictionary<string, string> parameterDict)
        {
            string formattedCodeBlock = codeBlock;
            if (!string.IsNullOrEmpty(codeBlock) && parameterDict != null)
            {
                string value = Regex.Replace(codeBlock, "^([^.]*).", "");
                string param = Regex.Replace(codeBlock, "[*^.].*", "");
                if (parameterDict.TryGetValue(param, out string parameter))
                {
                    formattedCodeBlock = $"{parameter}.{value}";
                }
            }
            return formattedCodeBlock;
        }

        internal static string FormatGlobalStatement(string codeBlock, IDictionary<string, string> replacements)
        {
            string formattedStatement = codeBlock;
            if (!string.IsNullOrEmpty(formattedStatement) && replacements != null && replacements.Any())
            {
                foreach (var key in replacements.Keys)
                {
                    replacements.TryGetValue(key, out string value);
                    if (!string.IsNullOrEmpty(value))
                    {
                        formattedStatement = formattedStatement.Replace(key, value);
                    }
                }
            }
            return formattedStatement;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="change"></param>
        /// <param name="variableDict"></param>
        /// <returns></returns>
        internal static CodeSnippet FormatCodeSnippet(CodeSnippet change, IDictionary<string, string> variableDict)
        {
            //format CodeSnippet fields for any variables or parameters.
            if (!string.IsNullOrEmpty(change.Block))
            {
                change.Block = FormatGlobalStatement(change.Block, variableDict);
            }
            if (!string.IsNullOrEmpty(change.Parent))
            {
                change.Parent = FormatGlobalStatement(change.Parent, variableDict);
            }
            if (!string.IsNullOrEmpty(change.CheckBlock))
            {
                change.CheckBlock = FormatGlobalStatement(change.CheckBlock, variableDict);
            }
            if (!string.IsNullOrEmpty(change.InsertAfter))
            {
                change.InsertAfter = FormatGlobalStatement(change.InsertAfter, variableDict);
            }
            if (change.InsertBefore != null && change.InsertBefore.Any())
            {
                for (int i = 0; i < change.InsertBefore.Count(); i++)
                {
                    change.InsertBefore[i] = FormatGlobalStatement(change.InsertBefore[i], variableDict);
                }
            }
            return change;
        }

        /// <summary>
        /// Trim ' ', '\r', '\n' and replace any whitespace with no spaces.
        /// </summary>
        /// <param name="statement"></param>
        /// <returns></returns>
        internal static string TrimStatement(string statement)
        {
            StringBuilder sb = new StringBuilder(statement);
            if (!string.IsNullOrEmpty(statement))
            {
                foreach (string replaceString in CodeSnippetTrimStrings)
                {
                    sb.Replace(replaceString, string.Empty);
                }
            }
            return sb.ToString();
        }

        internal static IDictionary<string, string> GetBuilderVariableIdentifier(SyntaxList<MemberDeclarationSyntax> members)
        {
            IDictionary<string, string> variables = new Dictionary<string, string>();
            if (members.Any())
            {
                foreach (var member in members)
                {
                    var memberString = TrimStatement(member.ToString());
                    if (memberString.Contains("=WebApplication.CreateBuilder"))
                    {
                        var start = 0;
                        if (memberString.Contains(VarIdentifier))
                        {
                            start = memberString.IndexOf(VarIdentifier) + VarIdentifier.Length;
                        }
                        else if (memberString.Contains(WebApplicationBuilderIdentifier))
                        {
                            start = memberString.IndexOf(WebApplicationBuilderIdentifier) + WebApplicationBuilderIdentifier.Length;
                        }
                        if (start > 0)
                        {
                            var end = memberString.IndexOf("=");
                            variables.Add("WebApplication.CreateBuilder", memberString.Substring(start, end - start));
                        }
                    }
                }
            }
            return variables;
        }

        internal static bool GlobalStatementExists(CompilationUnitSyntax root, GlobalStatementSyntax statement, string checkBlock = null)
        {
            if (root != null && statement != null)
            {
                var formattedStatementString = ProjectModifierHelper.TrimStatement(statement.ToString());
                bool foundStatement = root.Members.Where(st => ProjectModifierHelper.TrimStatement(st.ToString()).Contains(formattedStatementString)).Any();
                //if statement is not found due to our own mofications, check for a CheckBlock snippet 
                if (!string.IsNullOrEmpty(checkBlock) && !foundStatement)
                {
                    foundStatement = root.Members.Where(st => st.ToString().Trim(ProjectModifierHelper.CodeSnippetTrimChars).Contains(checkBlock.ToString().Trim(ProjectModifierHelper.CodeSnippetTrimChars))).Any();
                }
                return foundStatement;
            }
            return false;
        }

        internal static bool AttributeExists(string attribute, SyntaxList<AttributeListSyntax> attributeList)
        {
            if (attributeList.Any() && !string.IsNullOrEmpty(attribute))
            {
                return attributeList.Where(al => al.Attributes.Where(attr => attr.ToString().Equals(attribute, StringComparison.OrdinalIgnoreCase)).Any()).Any();
            }
            return false;
        }

        internal static bool StatementExists(BlockSyntax blockSyntaxNode, StatementSyntax statement)
        {
            if (blockSyntaxNode.Statements.Any(st => st.ToString().Contains(statement.ToString(), StringComparison.Ordinal)))
            {
                return true;
            }
            return false;
        }

        internal static ExpressionStatementSyntax AddSimpleMemberAccessExpression(ExpressionStatementSyntax expression, string codeSnippet, SyntaxTriviaList leadingTrivia, SyntaxTriviaList trailingTrivia)
        {
            if (!string.IsNullOrEmpty(codeSnippet) &&
                !expression
                    .ToString()
                    .Trim(ProjectModifierHelper.CodeSnippetTrimChars)
                    .Contains(
                        codeSnippet.Trim(ProjectModifierHelper.CodeSnippetTrimChars)))
            {
                var identifier = SyntaxFactory.IdentifierName(codeSnippet)
                            .WithTrailingTrivia(trailingTrivia);
                return expression
                    .WithExpression(SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        expression.Expression.WithTrailingTrivia(leadingTrivia),
                        identifier));
            }
            else
            {
                return null;
            }
        }

        //Filter out CodeSnippets that are invalid using FilterOptions
        internal static CodeSnippet[] FilterCodeSnippets(CodeSnippet[] codeSnippets, CodeChangeOptions options)
        {
            var filteredCodeSnippets = new HashSet<CodeSnippet>();
            if (codeSnippets != null && codeSnippets.Any() && options != null)
            {
                foreach (var codeSnippet in codeSnippets)
                {
                    if (FilterOptions(codeSnippet.Options, options))
                    {
                        filteredCodeSnippets.Add(codeSnippet);
                    }
                }
            }
            return filteredCodeSnippets.ToArray();
        }

        /// <summary>
        /// Filter Options string array to matching CodeChangeOptions.
        /// Primary use to filter out CodeBlocks and Files that apply in Microsoft Graph and Downstream API scenarios
        /// </summary>
        /// <param name="options">string [] in cm_*.json files for code modifications</param>
        /// <param name="codeChangeOptions">based on cli parameters</param>
        /// <returns>true if the CodeChangeOptions apply, false otherwise. </returns>
        internal static bool FilterOptions(string[] options, CodeChangeOptions codeChangeOptions)
        {
            //if no options are passed, CodeBlock is valid
            if (options == null)
            {
                return true;
            }

            //if options have a "Skio", every CodeBlock is invalid
            if (options.Contains(CodeChangeOptionStrings.Skip))
            {
                return false;
            }
            //an app will either support DownstreamApi, MicrosoftGraph, both, or neither.
            if (codeChangeOptions.DownstreamApi)
            {
                if (options.Contains(CodeChangeOptionStrings.DownstreamApi) ||
                    !options.Contains(CodeChangeOptionStrings.MicrosoftGraph))
                {
                    return true;
                }
            }
            if (codeChangeOptions.MicrosoftGraph)
            {
                if (options.Contains(CodeChangeOptionStrings.MicrosoftGraph) ||
                    !options.Contains(CodeChangeOptionStrings.DownstreamApi))
                {
                    return true;
                }
            }
            if (!codeChangeOptions.DownstreamApi && !codeChangeOptions.MicrosoftGraph)
            {
                if (options == null ||
                    (!options.Contains(CodeChangeOptionStrings.MicrosoftGraph) &&
                    !options.Contains(CodeChangeOptionStrings.DownstreamApi)))
                {
                    return true;
                }
            }
            return false;
        }

        internal static async Task<Document> AddTextToDocument(Document fileDoc, IEnumerable<CodeSnippet> codeChanges)
        {
            Document editedDoc = null;
            if (fileDoc != null && codeChanges != null && codeChanges.Any())
            {
                var sourceText = await fileDoc.GetTextAsync();
                var textToAdd = sourceText?.ToString();
                foreach (var change in codeChanges)
                {
                    if (!ProjectModifierHelper.TrimStatement(textToAdd).Contains(ProjectModifierHelper.TrimStatement(change.Block)))
                    {
                        textToAdd += change.Block;
                    }
                }
                if (!string.IsNullOrEmpty(textToAdd))
                {
                    var sourceTextToAdd = SourceText.From(textToAdd);
                    editedDoc = fileDoc.WithText(sourceTextToAdd);
                }
            }
            return editedDoc;
        }

        internal static async Task UpdateDocument(Document fileDoc, Document editedDocument, IConsoleLogger consoleLogger)
        {
            var classFileTxt = await editedDocument.GetTextAsync();
            File.WriteAllText(fileDoc.Name, classFileTxt.ToString());
            consoleLogger.LogMessage($"Modified {fileDoc.Name}.\n");
        }

        // Filter out CodeBlocks that are invalid using FilterOptions
        internal static CodeBlock[] FilterCodeBlocks(CodeBlock[] codeBlocks, CodeChangeOptions options)
        {
            var filteredCodeBlocks = new HashSet<CodeBlock>();
            if (codeBlocks != null && codeBlocks.Any() && options != null)
            {
                foreach (var codeBlock in codeBlocks)
                {
                    if (FilterOptions(codeBlock.Options, options))
                    {
                        filteredCodeBlocks.Add(codeBlock);
                    }
                }
            }
            return filteredCodeBlocks.ToArray();
        }
    }
}