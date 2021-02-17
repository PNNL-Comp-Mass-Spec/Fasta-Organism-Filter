﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using PRISM;
using ProteinFileReader;

namespace FastaOrganismFilter
{

    /// <summary>
    /// This class reads a fasta file and finds the organism info defined in the protein description lines
    /// It optionally creates a filtered fasta file containing only the proteins of interest
    /// </summary>
    /// <remarks>
    /// <para>Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Program started September 4, 2014
    /// Copyright 2014, Battelle Memorial Institute.  All Rights Reserved.
    /// </para>
    /// <para>
    /// E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
    /// Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/
    /// </para>
    /// <para>
    /// Licensed under the Apache License, Version 2.0; you may not use this file except
    /// in compliance with the License.  You may obtain a copy of the License at
    /// http://www.apache.org/licenses/LICENSE-2.0
    /// </para>
    /// </remarks>
    public class FilterFastaByOrganism
    {

        // Ignore Spelling: yyyy-MM-dd, hh:mm:ss, UniProt, enterica, subsp, serovar

        protected const int MAX_PROTEIN_DESCRIPTION_LENGTH = 7500;
        private readonly Regex mFindSpeciesTag;
        private readonly Regex mFindNextTag;

        /// <summary>
        /// Create a protein to organism map file
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool CreateProteinToOrganismMapFile { get; set; }

        /// <summary>
        /// Also search protein descriptions in addition to protein names
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool SearchProteinDescriptions { get; set; }

        /// <summary>
        /// Show additional messages when true, including which search term or RegEx resulted in a match
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool VerboseMode { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public FilterFastaByOrganism()
        {
            mFindSpeciesTag = new Regex("OS=(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            mFindNextTag = new Regex(" [a-z]+=", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        private static void AddRegExExpression(IDictionary<string, Regex> regExFilters, string expression)
        {
            if (!regExFilters.ContainsKey(expression))
            {
                regExFilters.Add(expression, new Regex(expression, RegexOptions.Compiled | RegexOptions.IgnoreCase));
            }
        }

        private string ExtractSpecies(string proteinDescription)
        {

            // Look for the first occurrence of OS=
            // Adding a bogus extra tag at the end in case the last official tag is OS=
            var match = mFindSpeciesTag.Match(proteinDescription + " XX=Ignore");
            if (match.Success)
            {
                var speciesTag = match.Groups[1].Value;
                var match2 = mFindNextTag.Match(speciesTag);
                if (match2.Success)
                {
                    var species = speciesTag.Substring(0, match2.Index);
                    return species;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Search for the organism info, assuming it is the last text seen between two square brackets
        /// </summary>
        /// <param name="proteinDescription"></param>
        /// <returns></returns>
        /// <remarks>
        /// However, there are exceptions we have to consider, for example
        /// [Salmonella enterica subsp. enterica serovar 4,[5],12:i:-]
        /// </remarks>
        private string ExtractOrganism(string proteinDescription)
        {
            var indexEnd = proteinDescription.LastIndexOf(']');
            if (indexEnd >= 0)
            {
                // Back track until we find [
                // But, watch for ] while back-tracking
                var subLevel = 1;
                for (var index = indexEnd - 1; index >= 0; index -= 1)
                {
                    var chChar = proteinDescription.Substring(index, 1);
                    if (chChar == "]")
                    {
                        subLevel += 1;
                    }
                    else if (chChar == "[")
                    {
                        subLevel -= 1;
                        if (subLevel == 0)
                        {
                            var organism = proteinDescription.Substring(index + 1, indexEnd - index - 1);
                            return organism;
                        }
                    }
                }

                return proteinDescription.Substring(0, indexEnd - 1);
            }

            return string.Empty;
        }

        public bool FilterFastaOneOrganism(string inputFilePath, string organismName, string outputDirectoryPath)
        {
            try
            {
                DirectoryInfo outputDirectory = null;
                if (!ValidateInputAndOutputDirectories(inputFilePath, ref outputDirectoryPath, out outputDirectory))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(organismName))
                {
                    ConsoleMsgUtils.ShowError("Organism name is empty");
                    return false;
                }

                // Keys in this dictionary are the organism names to filter on; values are meaningless integers
                // The reason for using a dictionary is to provide fast lookups, but without case sensitivity
                var organismNameFilters = new Dictionary<string, int>();
                var regExFilters = new Dictionary<string, Regex>();
                if (organismName.Contains("*"))
                {
                    AddRegExExpression(regExFilters, organismName.Replace("*", ".+"));
                }
                else
                {
                    organismNameFilters.Add(organismName, 0);
                }

                var badChars = new List<char>() { ' ', '\\', '/', ':', '*', '?', '.', '<', '>', '|' };
                var outputFileSuffix = "_";
                foreach (var chCharacter in organismName)
                {
                    if (badChars.Contains(chCharacter))
                    {
                        outputFileSuffix += '_';
                    }
                    else
                    {
                        outputFileSuffix += chCharacter;
                    }
                }

                outputFileSuffix = outputFileSuffix.TrimEnd('_');
                var success = FilterFastaByOrganismWork(inputFilePath, outputDirectory, organismNameFilters, regExFilters, outputFileSuffix);
                return success;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error in FindOrganismsInFasta", ex);
                return false;
            }
        }

        public bool FilterFastaByOrganismName(string inputFilePath, string organismListFilePath, string outputDirectoryPath)
        {
            try
            {
                DirectoryInfo outputDirectory = null;
                if (!ValidateInputAndOutputDirectories(inputFilePath, ref outputDirectoryPath, out outputDirectory))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(organismListFilePath))
                {
                    ConsoleMsgUtils.ShowError("Organism list file not defined");
                    return false;
                }

                var organismListFile = new FileInfo(organismListFilePath);
                if (!organismListFile.Exists)
                {
                    ConsoleMsgUtils.ShowError("Organism list file not found: " + organismListFile.FullName);
                    return false;
                }

                ShowMessage("Loading the organism name filters from " + organismListFile.Name);

                // Keys in this dictionary are the organism names to filter on; values are meaningless integers
                // The reason for using a dictionary is to provide fast lookups, but without case sensitivity
                Dictionary<string, int> textFilters = null;
                Dictionary<string, Regex> regExFilters = null;
                if (!ReadNameFilterFile(organismListFile, out textFilters, out regExFilters))
                {
                    return false;
                }

                if (textFilters.Count == 0 && regExFilters.Count == 0)
                {
                    ConsoleMsgUtils.ShowError("Organism list file is empty: " + organismListFile.FullName);
                    return false;
                }

                var success = FilterFastaByOrganismWork(inputFilePath, outputDirectory, textFilters, regExFilters);
                return success;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error in FilterFastaByOrganism", ex);
                return false;
            }
        }

        private bool FilterFastaByOrganismWork(string inputFilePath, DirectoryInfo outputDirectory, IDictionary<string, int> textFilters, Dictionary<string, Regex> regExFilters)
        {
            return FilterFastaByOrganismWork(inputFilePath, outputDirectory, textFilters, regExFilters, "");
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private bool FilterFastaByOrganismWork(string inputFilePath, DirectoryInfo outputDirectory, IDictionary<string, int> organismNameFilters, Dictionary<string, Regex> regExFilters, string outputFileSuffix)
        {
            var reader = new FastaFileReader();
            var lastProgressTime = DateTime.UtcNow;
            if (!reader.OpenFile(inputFilePath))
            {
                ConsoleMsgUtils.ShowError("Error opening the fasta file; aborting");
                return false;
            }

            ShowMessage("Parsing " + Path.GetFileName(inputFilePath));
            if (string.IsNullOrWhiteSpace(outputFileSuffix))
            {
                outputFileSuffix = "_Filtered";
            }

            var baseName = Path.GetFileNameWithoutExtension(inputFilePath);
            var filteredFastaFilePath = Path.Combine(outputDirectory.FullName, baseName + outputFileSuffix + ".fasta");
            StreamWriter matchInfoWriter = null;
            if (VerboseMode)
            {
                var matchInfoFilePath = Path.Combine(outputDirectory.FullName, baseName + outputFileSuffix + "_MatchInfo.txt");
                matchInfoWriter = new StreamWriter(new FileStream(matchInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));
                matchInfoWriter.WriteLine("Protein\tFilterMatch\tRegEx");
            }

            ShowMessage("Creating " + Path.GetFileName(filteredFastaFilePath));
            using (var writer = new StreamWriter(new FileStream(filteredFastaFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                while (reader.ReadNextProteinEntry())
                {
                    var species = ExtractSpecies(reader.ProteinDescription);
                    var keepProtein = false;
                    var keepProteinFromDescription = false;
                    if (!string.IsNullOrEmpty(species))
                    {
                        // UniProt Fasta file with OS= entries
                        keepProtein = IsExactOrRegexMatch(reader.ProteinName, species, organismNameFilters, regExFilters, VerboseMode, matchInfoWriter);
                    }
                    else
                    {
                        var organism = ExtractOrganism(reader.ProteinDescription);
                        if (!string.IsNullOrEmpty(organism))
                        {
                            // Match organism name within square brackets
                            keepProtein = IsExactOrRegexMatch(reader.ProteinName, organism, organismNameFilters, regExFilters, VerboseMode, matchInfoWriter);
                        }

                        if (!keepProtein)
                        {
                            // Match the entire protein description
                            keepProteinFromDescription = IsExactOrRegexMatch(reader.ProteinName, reader.ProteinDescription, organismNameFilters, regExFilters, VerboseMode, matchInfoWriter);
                        }
                    }

                    if (keepProtein | keepProteinFromDescription)
                    {
                        var argwriter = writer;
                        WriteFastaFileEntry(ref argwriter, reader);
                    }

                    if (DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds < 10d)
                        continue;
                    lastProgressTime = DateTime.UtcNow;
                    if (VerboseMode)
                        Console.WriteLine();
                    if (VerboseMode)
                        Console.WriteLine("--------------------------------------------");
                    ReportProgress("Working: " + reader.PercentFileProcessed() + "% complete");
                    if (VerboseMode)
                        Console.WriteLine("--------------------------------------------");
                    if (VerboseMode)
                        Console.WriteLine();
                }
            }

            if (matchInfoWriter is object)
            {
                matchInfoWriter.Close();
            }

            return true;
        }

        private static bool IsExactOrRegexMatch(string proteinName, string textToSearch, IDictionary<string, int> itemsToMatchExactly, Dictionary<string, Regex> regExFilters, bool showMessages, TextWriter matchInfoWriter)
        {
            var keepProtein = false;
            if (itemsToMatchExactly.ContainsKey(textToSearch))
            {
                keepProtein = true;
                if (showMessages)
                {
                    Console.WriteLine("Protein " + proteinName + " matched " + textToSearch);
                }

                if (matchInfoWriter is object)
                {
                    matchInfoWriter.WriteLine(proteinName + '\t' + textToSearch);
                }
            }
            else
            {
                foreach (var regExSpec in regExFilters)
                {
                    var match = regExSpec.Value.Match(textToSearch);
                    if (match.Success)
                    {
                        keepProtein = true;
                        if (showMessages)
                        {
                            var contextIndexStart = match.Index - 5;
                            var contextIndexEnd = match.Index + match.Value.Length + 10;
                            if (contextIndexStart < 0)
                                contextIndexStart = 0;
                            if (contextIndexEnd >= textToSearch.Length)
                                contextIndexEnd = textToSearch.Length - 1;
                            Console.WriteLine("Protein " + proteinName + " matched " + match.Value + " in: " + textToSearch.Substring(contextIndexStart, contextIndexEnd - contextIndexStart));
                        }

                        if (matchInfoWriter is object)
                        {
                            matchInfoWriter.WriteLine(proteinName + '\t' + match.Value + '\t' + regExSpec.Key);
                        }

                        break;
                    }
                }
            }

            return keepProtein;
        }

        public bool FilterFastaByProteinName(string inputFilePath, string proteinListFile, string outputDirectoryPath)
        {
            try
            {
                DirectoryInfo outputDirectory = null;
                if (!ValidateInputAndOutputDirectories(inputFilePath, ref outputDirectoryPath, out outputDirectory))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(proteinListFile))
                {
                    ConsoleMsgUtils.ShowError("Protein list file not defined");
                    return false;
                }

                var fiProteinListFile = new FileInfo(proteinListFile);
                if (!fiProteinListFile.Exists)
                {
                    ConsoleMsgUtils.ShowError("Protein list file not found: " + fiProteinListFile.FullName);
                    return false;
                }

                ShowMessage("Loading the protein name filters from " + fiProteinListFile.Name);

                // Keys in this dictionary are the protein names to filter on; values are meaningless integers
                // The reason for using a dictionary is to provide fast lookups, but without case sensitivity
                Dictionary<string, int> textFilters = null;
                Dictionary<string, Regex> regExFilters = null;
                if (!ReadNameFilterFile(fiProteinListFile, out textFilters, out regExFilters))
                {
                    return false;
                }

                if (textFilters.Count == 0 && regExFilters.Count == 0)
                {
                    ConsoleMsgUtils.ShowError("Protein list file is empty: " + fiProteinListFile.FullName);
                    return false;
                }

                var success = FilterFastaByProteinWork(inputFilePath, outputDirectory, textFilters, regExFilters);
                return success;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error in FilterProteinName", ex);
                return false;
            }
        }

        private bool FilterFastaByProteinWork(string inputFilePath, DirectoryInfo outputDirectory, IDictionary<string, int> textFilters, Dictionary<string, Regex> regExFilters)
        {
            return FilterFastaByProteinWork(inputFilePath, outputDirectory, textFilters, regExFilters, "");
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private bool FilterFastaByProteinWork(string inputFilePath, DirectoryInfo outputDirectory, IDictionary<string, int> textFilters, Dictionary<string, Regex> regExFilters, string outputFileSuffix)
        {
            var reader = new FastaFileReader();
            var lastProgressTime = DateTime.UtcNow;
            if (!reader.OpenFile(inputFilePath))
            {
                ConsoleMsgUtils.ShowError("Error opening the fasta file; aborting");
                return false;
            }

            ShowMessage("Parsing " + Path.GetFileName(inputFilePath));
            if (string.IsNullOrWhiteSpace(outputFileSuffix))
            {
                outputFileSuffix = "_Filtered";
            }

            var baseName = Path.GetFileNameWithoutExtension(inputFilePath);
            var filteredFastaFilePath = Path.Combine(outputDirectory.FullName, baseName + outputFileSuffix + ".fasta");
            StreamWriter matchInfoWriter = null;
            if (VerboseMode)
            {
                var matchInfoFilePath = Path.Combine(outputDirectory.FullName, baseName + outputFileSuffix + "_MatchInfo.txt");
                matchInfoWriter = new StreamWriter(new FileStream(matchInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));
                matchInfoWriter.WriteLine("Protein\tFilterMatch\tRegEx");
            }

            ShowMessage("Creating " + Path.GetFileName(filteredFastaFilePath));
            using (var writer = new StreamWriter(new FileStream(filteredFastaFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                while (reader.ReadNextProteinEntry())
                {
                    var keepProtein = IsExactOrRegexMatch(reader.ProteinName, reader.ProteinName, textFilters, regExFilters, VerboseMode, matchInfoWriter);
                    var keepProteinFromDescription = false;
                    if (!keepProtein && SearchProteinDescriptions)
                    {
                        keepProteinFromDescription = IsExactOrRegexMatch(reader.ProteinName, reader.ProteinDescription, textFilters, regExFilters, VerboseMode, matchInfoWriter);
                    }

                    if (keepProtein | keepProteinFromDescription)
                    {
                        var argwriter = writer;
                        WriteFastaFileEntry(ref argwriter, reader);
                    }

                    if (DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds < 10d)
                        continue;
                    lastProgressTime = DateTime.UtcNow;
                    if (VerboseMode)
                        Console.WriteLine();
                    if (VerboseMode)
                        Console.WriteLine("--------------------------------------------");
                    ReportProgress("Working: " + reader.PercentFileProcessed() + "% complete");
                    if (VerboseMode)
                        Console.WriteLine("--------------------------------------------");
                    if (VerboseMode)
                        Console.WriteLine();
                }
            }

            if (matchInfoWriter is object)
            {
                matchInfoWriter.Close();
            }

            return true;
        }

        public bool FindOrganismsInFasta(string inputFilePath, string outputDirectoryPath)
        {
            try
            {
                var reader = new FastaFileReader();
                var lastProgressTime = DateTime.UtcNow;
                DirectoryInfo outputDirectory = null;
                if (!ValidateInputAndOutputDirectories(inputFilePath, ref outputDirectoryPath, out outputDirectory))
                {
                    return false;
                }

                // Key is organism name, value is protein usage count
                var lstOrganisms = new Dictionary<string, int>();
                if (!reader.OpenFile(inputFilePath))
                {
                    ConsoleMsgUtils.ShowError("Error opening the fasta file; aborting");
                    return false;
                }

                ShowMessage("Parsing " + Path.GetFileName(inputFilePath));
                var baseName = Path.GetFileNameWithoutExtension(inputFilePath);
                StreamWriter mapFileWriter = null;
                var mapFilePath = Path.Combine(outputDirectory.FullName, baseName + "_ProteinOrganismMap.txt");
                if (CreateProteinToOrganismMapFile)
                {
                    ShowMessage("Creating " + Path.GetFileName(mapFilePath));
                    mapFileWriter = new StreamWriter(new FileStream(mapFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));
                    mapFileWriter.WriteLine("Protein\tOrganism");
                }

                while (reader.ReadNextProteinEntry())
                {
                    var organism = ExtractSpecies(reader.ProteinDescription);
                    if (string.IsNullOrEmpty(organism))
                    {
                        organism = ExtractOrganism(reader.ProteinDescription);
                    }

                    if (string.IsNullOrWhiteSpace(organism))
                    {
                        ShowMessage(" Warning: Organism not found for " + reader.ProteinName);
                        continue;
                    }

                    int proteinCount;
                    if (lstOrganisms.TryGetValue(organism, out proteinCount))
                    {
                        lstOrganisms[organism] = proteinCount + 1;
                    }
                    else
                    {
                        lstOrganisms.Add(organism, 1);
                    }

                    if (mapFileWriter is object)
                    {
                        mapFileWriter.WriteLine(reader.ProteinName + '\t' + organism);
                    }

                    if (DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds >= 10d)
                    {
                        lastProgressTime = DateTime.UtcNow;
                        if (VerboseMode)
                            Console.WriteLine();
                        if (VerboseMode)
                            Console.WriteLine("--------------------------------------------");
                        ReportProgress("Working: " + reader.PercentFileProcessed() + "% complete");
                        if (VerboseMode)
                            Console.WriteLine("--------------------------------------------");
                        if (VerboseMode)
                            Console.WriteLine();
                    }
                }

                if (mapFileWriter is object)
                {
                    mapFileWriter.Close();
                }

                var organismSummaryFilePath = Path.Combine(outputDirectory.FullName, baseName + "_OrganismSummary.txt");
                ShowMessage("Creating " + Path.GetFileName(organismSummaryFilePath));

                // Now write out the unique list of organisms
                using (var summaryWriter = new StreamWriter(new FileStream(organismSummaryFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    summaryWriter.WriteLine("Organism\tProteins\tGenus\tSpecies");
                    var organismsSorted = from item in lstOrganisms
                                          select item;
                    var lstSquareBrackets = new char[] { '[', ']' };
                    foreach (var organism in organismsSorted)
                    {
                        var genus = string.Empty;
                        var species = string.Empty;
                        var nameParts = organism.Key.Split(' ').ToList();
                        if (nameParts.Count > 0)
                        {
                            genus = nameParts[0].Trim(lstSquareBrackets);
                            if (nameParts.Count > 1)
                            {
                                species = nameParts[1];
                            }
                        }

                        summaryWriter.WriteLine(organism.Key + '\t' + organism.Value + '\t' + genus + '\t' + species);
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error in FindOrganismsInFasta", ex);
                return false;
            }

            return true;
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private bool ReadNameFilterFile(FileInfo nameListFile, out Dictionary<string, int> textFilters, out Dictionary<string, Regex> regExFilters)
        {
            textFilters = new Dictionary<string, int>((int)StringComparison.CurrentCultureIgnoreCase);
            regExFilters = new Dictionary<string, Regex>();
            var lineNumber = 0;
            try
            {
                using (var reader = new StreamReader(new FileStream(nameListFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    while (!reader.EndOfStream)
                    {
                        var dataLine = reader.ReadLine();
                        lineNumber += 1;
                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        // Check for "RegEx:"
                        if (dataLine.StartsWith("RegEx:"))
                        {
                            var regExFilter = dataLine.Substring("RegEx:".Length);
                            if (string.IsNullOrWhiteSpace(regExFilter))
                            {
                                ShowMessage("  Warning: empty RegEx filter defined on line " + lineNumber);
                                continue;
                            }

                            AddRegExExpression(regExFilters, regExFilter);
                        }
                        else if (!textFilters.ContainsKey(dataLine))
                        {
                            textFilters.Add(dataLine, lineNumber);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error in ReadNameFilterFile at line " + lineNumber, ex.Message);
                return false;
            }

            return true;
        }

        private void ReportProgress(string strProgress)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + " " + strProgress);
        }

        private void ShowMessage(string message)
        {
            Console.WriteLine(message);
        }

        private bool ValidateInputAndOutputDirectories(string inputFilePath, ref string outputDirectoryPath, out DirectoryInfo outputDirectory)
        {
            var fiSourceFile = new FileInfo(inputFilePath);
            if (!fiSourceFile.Exists)
            {
                ConsoleMsgUtils.ShowError("Source file not found: " + inputFilePath);
                outputDirectory = null;
                return false;
            }

            if (string.IsNullOrWhiteSpace(outputDirectoryPath))
            {
                outputDirectoryPath = fiSourceFile.Directory.FullName;
            }

            outputDirectory = ValidateOutputDirectory(ref outputDirectoryPath);
            if (outputDirectory is null)
                return false;
            if ((outputDirectory.FullName ?? "") != (fiSourceFile.Directory.FullName ?? ""))
            {
                ShowMessage("Output directory: " + outputDirectory.FullName);
            }

            return true;
        }

        private DirectoryInfo ValidateOutputDirectory(ref string outputDirectoryPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outputDirectoryPath))
                {
                    outputDirectoryPath = ".";
                }

                var outputDirectory = new DirectoryInfo(outputDirectoryPath);
                if (!outputDirectory.Exists)
                {
                    outputDirectory.Create();
                }

                return outputDirectory;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error validating the output directory", ex);
                return null;
            }
        }

        private void WriteFastaFileEntry(ref StreamWriter writer, ProteinFileReaderBaseClass reader)
        {
            const int RESIDUES_PER_LINE = 60;
            var headerLine = reader.HeaderLine;
            var spaceIndex = headerLine.IndexOf(' ');
            if (spaceIndex > 0 && headerLine.Length - spaceIndex >= MAX_PROTEIN_DESCRIPTION_LENGTH)
            {
                headerLine = headerLine.Substring(0, spaceIndex) + " " + headerLine.Substring(spaceIndex + 1, MAX_PROTEIN_DESCRIPTION_LENGTH);
            }

            writer.WriteLine(">" + headerLine);

            // Now write out the residues
            var intStartIndex = 0;
            var proteinSequence = reader.ProteinSequence;
            var intLength = proteinSequence.Length;
            while (intStartIndex < intLength)
            {
                if (intStartIndex + RESIDUES_PER_LINE <= intLength)
                {
                    writer.WriteLine(proteinSequence.Substring(intStartIndex, RESIDUES_PER_LINE));
                }
                else
                {
                    writer.WriteLine(proteinSequence.Substring(intStartIndex));
                }

                intStartIndex += RESIDUES_PER_LINE;
            }
        }
    }
}