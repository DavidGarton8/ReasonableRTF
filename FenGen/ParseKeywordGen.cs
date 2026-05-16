using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static FenGen.Misc;

namespace FenGen;

internal static class ParseKeywordGen
{
    private sealed class SourceLine
    {
        internal readonly string Line;
        internal readonly bool RemoveForFastVersion;

        public SourceLine(string line, bool removeForFastVersion)
        {
            Line = line;
            RemoveForFastVersion = removeForFastVersion;
        }
    }

    internal static void Generate(string sourceFile, List<string> destFiles)
    {
        SyntaxNode[] nodes = GetNodes(sourceFile);

        (MemberDeclarationSyntax member, AttributeSyntax attr) = GetAttrMarkedItem(
            nodes,
            SyntaxKind.MethodDeclaration,
            GenAttributes.FenGen_ParseKeywordAttribute);

        const int reqArgCount = 4;

        if (attr.ArgumentList is not { Arguments.Count: reqArgCount })
        {
            ThrowErrorAndTerminate(nameof(GenAttributes.FenGen_ParseKeywordAttribute) + " had wrong number of args (should be " + reqArgCount + ")");
            return;
        }

        string getByteFunctionName = GetStringParamValue(attr, 0);
        string bufferRefIncrementFunctionName = GetStringParamValue(attr, 1);
        string incrementFunctionName = GetStringParamValue(attr, 2);
        string bufferRefName = GetStringParamValue(attr, 3);

        if (member is not MethodDeclarationSyntax method)
        {
            ThrowErrorAndTerminate("Attribute " + GenAttributes.FenGen_ParseKeywordAttribute + " was not on a function");
            return;
        }

        TextLineCollection methodLines = method.GetText().Lines;
        List<SourceLine> sourceLines = new();
        bool inSourceLinesSection = false;
        bool removeNextLine = false;
        for (int i = 0; i < methodLines.Count; i++)
        {
            TextLine line = methodLines[i];
            string lineStr = line.ToString();

            if (inSourceLinesSection)
            {
                if (IsFenGenNotationLine(lineStr, "[FenGen:ScalarKeywordParseSection:Source:End]"))
                {
                    break;
                }
                else
                {
                    if (removeNextLine)
                    {
                        removeNextLine = false;
                        sourceLines.Add(new SourceLine(lineStr, true));
                        continue;
                    }

                    if (IsFenGenNotationLine(lineStr, "[FenGen:Fast:RemoveLine]"))
                    {
                        // This line should always be removed because it's markup, so just don't add it in the
                        // first place.
                        removeNextLine = true;
                        continue;
                    }

                    sourceLines.Add(new SourceLine(lineStr, false));
                }
            }
            else if (IsFenGenNotationLine(lineStr, "[FenGen:ScalarKeywordParseSection:Source:Begin]"))
            {
                inSourceLinesSection = true;
            }
        }

        foreach (string destFile in destFiles)
        {
            List<string> destLines = File.ReadAllLines(destFile).ToList();
            for (int i = 0; i < destLines.Count; i++)
            {
                string line = destLines[i];
                if (IsFenGenNotationLine(line, "[FenGen:ScalarKeywordParseSection:Slow:Dest:Begin]"))
                {
                    CopyLines(sourceLines, destLines, destFile, i, "Slow", getByteFunctionName, incrementFunctionName, bufferRefIncrementFunctionName, bufferRefName);
                    break;
                }
                else if (IsFenGenNotationLine(line, "[FenGen:ScalarKeywordParseSection:Fast:Dest:Begin]"))
                {
                    CopyLines(sourceLines, destLines, destFile, i, "Fast", getByteFunctionName, incrementFunctionName, bufferRefIncrementFunctionName, bufferRefName);
                    break;
                }
            }
        }
    }

    private static void CopyLines(
        List<SourceLine> sourceLines,
        List<string> destLines,
        string destFile,
        int i,
        string version,
        string getByteFunctionName,
        string incrementFunctionName,
        string bufferRefIncrementFunctionName,
        string bufferRefName)
    {
        for (int subI = i + 1; subI < destLines.Count; subI++)
        {
            string subLine = destLines[subI];
            if (IsFenGenNotationLine(subLine, "[FenGen:ScalarKeywordParseSection:" + version + ":Dest:End]"))
            {
                for (int copyI = sourceLines.Count - 1; copyI >= 0; copyI--)
                {
                    SourceLine? sourceLine = sourceLines[copyI];
                    string line = sourceLine.Line;
                    if (version == "Fast")
                    {
                        if (sourceLine.RemoveForFastVersion)
                        {
                            continue;
                        }

                        line = line.Replace(
                            getByteFunctionName + "(" + incrementFunctionName + "())",
                            bufferRefIncrementFunctionName + "(ref " + bufferRefName + ")"
                        );
                    }
                    destLines.Insert(subI, line);
                }

                File.WriteAllLines(destFile, destLines);

                return;
            }
            else
            {
                destLines.RemoveAt(subI);
                subI--;
            }
        }
    }

    private static bool IsFenGenNotationLine(string line, string value)
    {
        string lineT = line.Trim();
        return lineT.StartsWithO("//") && lineT.TrimStart('/').TrimStart(' ') == value;
    }
}
