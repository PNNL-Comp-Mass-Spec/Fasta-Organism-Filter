using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using PRISM;
using ProteinFileReader;

namespace FastaOrganismFilter
{
    /// <summary>
    /// This class reads a FASTA file and finds the organism info defined in the protein description lines
    /// It optionally creates a filtered FASTA file containing only the proteins of interest
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Program started September 4, 2014
    /// Copyright 2014, Battelle Memorial Institute.  All Rights Reserved.
    /// </para>
    /// <para>
    /// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
    /// Website: https://github.com/PNNL-Comp-Mass-Spec/ or https://panomics.pnnl.gov/ or https://www.pnnl.gov/integrative-omics
    /// </para>
    /// <para>
    /// Licensed under the 2-Clause BSD License; you may not use this file except
    /// in compliance with the License.  You may obtain a copy of the License at
    /// https://opensource.org/licenses/BSD-2-Clause
    /// </para>
    /// </remarks>
    public class FilterFastaByOrganism
    {
        // Ignore Spelling: yyyy-MM-dd, hh:mm:ss, UniProt, enterica, subsp, serovar

        private const int MAX_PROTEIN_DESCRIPTION_LENGTH = 7500;

        private readonly Regex mFindSpeciesTag;
        private readonly Regex mFindTaxonomyTag;
        private readonly Regex mFindNextTag;

        private enum FilterModes
        {
            OrganismName = 0,
            ProteinName = 1,
            TaxonomyID = 2
        }

        /// <summary>
        /// Processing Options
        /// </summary>
        public FastaFilterOptions Options { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        public FilterFastaByOrganism(FastaFilterOptions options)
        {
            Options = options;

            mFindSpeciesTag = new Regex("OS=(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            mFindTaxonomyTag = new Regex(@"OX=(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            mFindNextTag = new Regex(" [a-z]+=", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        private static void AddRegExExpression(IDictionary<string, Regex> regExFilters, string expression)
        {
            if (!regExFilters.ContainsKey(expression))
            {
                regExFilters.Add(expression, new Regex(expression, RegexOptions.Compiled | RegexOptions.IgnoreCase));
            }
        }

        private string ExtractSpecies(string proteinDescription, out int taxonomyId)
        {
            taxonomyId = ExtractTaxonomyID(proteinDescription);

            // Look for the first occurrence of OS=
            // Adding a bogus extra tag at the end in case the last official tag is OS=
            var organismMatch = mFindSpeciesTag.Match(proteinDescription + " XX=Ignore");

            if (organismMatch.Success)
            {
                var speciesTag = organismMatch.Groups[1].Value;
                var match2 = mFindNextTag.Match(speciesTag);
                if (match2.Success)
                {
                    return speciesTag.Substring(0, match2.Index);
                }
            }

            return string.Empty;
        }

        private int ExtractTaxonomyID(string proteinDescription)
        {
            // Look for the first occurrence of OX=
            var taxonomyMatch = mFindTaxonomyTag.Match(proteinDescription);

            if (!taxonomyMatch.Success)
            {
                return 0;
            }

            return int.TryParse(taxonomyMatch.Groups[1].Value, out var taxId) ? taxId : 0;
        }

        /// <summary>
        /// Search for the organism info, assuming it is the last text seen between two square brackets
        /// </summary>
        /// <param name="proteinDescription"></param>
        /// <returns>Organism name if found, otherwise empty string</returns>
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
                for (var index = indexEnd - 1; index >= 0; --index)
                {
                    var chChar = proteinDescription.Substring(index, 1);
                    if (chChar == "]")
                    {
                        subLevel++;
                    }
                    else if (chChar == "[")
                    {
                        subLevel--;
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

        /// <summary>
        /// Filter the FASTA file using the specified organism name
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="organismName"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <returns>True if successful, false if an error</returns>
        public bool FilterFastaOneOrganism(string inputFilePath, string organismName, string outputDirectoryPath)
        {
            try
            {
                if (!ValidateInputAndOutputDirectories(inputFilePath, ref outputDirectoryPath, out var outputDirectory))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(organismName))
                {
                    ConsoleMsgUtils.ShowError("Organism name is empty");
                    return false;
                }

                var organismNameFilters = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                var regExFilters = new Dictionary<string, Regex>();
                var taxonomyIDs = new SortedSet<int>();

                if (organismName.Contains("*"))
                {
                    AddRegExExpression(regExFilters, organismName.Replace("*", ".+"));
                }
                else
                {
                    organismNameFilters.Add(organismName);
                }

                var badChars = new List<char> { ' ', '\\', '/', ':', '*', '?', '.', '<', '>', '|' };
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
                var success = FilterFastaByOrganismOrTaxonomyId(
                    inputFilePath, outputDirectory, FilterModes.OrganismName, organismNameFilters, regExFilters, taxonomyIDs, outputFileSuffix);

                return success;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error in FilterFastaOneOrganism", ex);
                return false;
            }
        }

        /// <summary>
        /// Filter the FASTA file using the organism names specified in the organism list file
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="organismListFilePath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <returns>True if successful, false if an error</returns>
        public bool FilterFastaByOrganismName(string inputFilePath, string organismListFilePath, string outputDirectoryPath)
        {
            try
            {
                Console.WriteLine();

                if (!ValidateInputAndOutputDirectories(inputFilePath, ref outputDirectoryPath, out var outputDirectory))
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

                if (!ReadNameFilterFile(organismListFile, FilterModes.OrganismName, out var organismNameFilters, out var regExFilters, out var taxonomyIDs))
                {
                    return false;
                }

                if (organismNameFilters.Count == 0 && regExFilters.Count == 0 && taxonomyIDs.Count == 0)
                {
                    ConsoleMsgUtils.ShowError("Organism list file is empty: " + organismListFile.FullName);
                    return false;
                }

                var success = FilterFastaByOrganismOrTaxonomyId(
                    inputFilePath, outputDirectory, FilterModes.OrganismName, organismNameFilters, regExFilters, taxonomyIDs);

                return success;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error in FilterFastaByOrganismName", ex);
                return false;
            }
        }

        /// <summary>
        /// Filter the FASTA file using the taxonomy ID values specified in the taxonomy ID list file
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="taxonomyIdListFilePath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <returns>True if successful, false if an error</returns>
        public bool FilterFastaByTaxonomyID(string inputFilePath, string taxonomyIdListFilePath, string outputDirectoryPath)
        {
            try
            {
                Console.WriteLine();

                if (!ValidateInputAndOutputDirectories(inputFilePath, ref outputDirectoryPath, out var outputDirectory))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(taxonomyIdListFilePath))
                {
                    ConsoleMsgUtils.ShowError("Taxonomy ID list file not defined");
                    return false;
                }

                var taxonomyIdListFile = new FileInfo(taxonomyIdListFilePath);
                if (!taxonomyIdListFile.Exists)
                {
                    ConsoleMsgUtils.ShowError("Taxonomy ID list file not found: " + taxonomyIdListFile.FullName);
                    return false;
                }

                ShowMessage("Loading taxonomy IDs from " + taxonomyIdListFile.Name);

                var taxonomyIDs = new SortedSet<int>();

                var lineNumber = 0;
                try
                {
                    using var reader = new StreamReader(new FileStream(taxonomyIdListFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read));

                    while (!reader.EndOfStream)
                    {
                        var dataLine = reader.ReadLine();

                        lineNumber++;
                        if (string.IsNullOrWhiteSpace(dataLine))
                            continue;

                        if (int.TryParse(dataLine, out var taxId))
                        {
                            taxonomyIDs.Add(taxId);
                        }
                        else if (lineNumber > 1)
                        {
                            // Warn of lines that do not have an integer, but skip the first line in case it's a header
                            ShowMessage("  Warning: line {0:N0} is not an integer: {1}", lineNumber, dataLine);
                        }
                    }

                    if (taxonomyIDs.Count > 0)
                    {
                        ShowMessage("Read {0:N0} taxonomy ID value{1} from {2}",
                            taxonomyIDs.Count, Pluralize(taxonomyIDs.Count), taxonomyIdListFile.Name);
                    }
                }
                catch (Exception ex)
                {
                    ConsoleMsgUtils.ShowError("Error in FilterFastaByTaxonomyID while reading the input file, line " + lineNumber, ex.Message);
                    return false;
                }

                if (taxonomyIDs.Count == 0)
                {
                    ConsoleMsgUtils.ShowError("Taxonomy ID list file is empty: " + taxonomyIdListFile.FullName);
                    return false;
                }

                var organismNameFilters = new SortedSet<string>();
                var regExFilters = new Dictionary<string, Regex>();

                var success = FilterFastaByOrganismOrTaxonomyId(
                    inputFilePath, outputDirectory, FilterModes.TaxonomyID, organismNameFilters, regExFilters, taxonomyIDs);

                return success;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error in FilterFastaByTaxonomyID", ex);
                return false;
            }
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        /// <summary>
        /// Filter the FASTA file based on the data in organismNameFilters, regExFilters, or taxonomyIDs
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="outputDirectory"></param>
        /// <param name="filterMode">Used to indicate the type of filter being used</param>
        /// <param name="organismNameFilters">Sorted set of organism names</param>
        /// <param name="regExFilters">Dictionary where keys are the RegEx text and values are compiled RegEx instances</param>
        /// <param name="taxonomyIDs">Sorted set of taxonomy IDs</param>
        /// <param name="outputFileSuffix"></param>
        /// <returns>True if successful, false if an error</returns>
        private bool FilterFastaByOrganismOrTaxonomyId(
            string inputFilePath,
            DirectoryInfo outputDirectory,
            FilterModes filterMode,
            ICollection<string> organismNameFilters,
            Dictionary<string, Regex> regExFilters,
            ICollection<int> taxonomyIDs,
            string outputFileSuffix = "")
        {
            Console.WriteLine();

            var reader = new FastaFileReader();

            if (!reader.OpenFile(inputFilePath))
            {
                ConsoleMsgUtils.ShowError("Error opening the FASTA file; aborting");
                return false;
            }

            var lastProgressTime = DateTime.UtcNow;
            var proteinsRead = 0;
            var proteinsWritten = 0;

            ShowMessage("Parsing " + Path.GetFileName(inputFilePath));
            ShowMessage("Filtering by " + GetFilterModeDescription(filterMode));

            if (string.IsNullOrWhiteSpace(outputFileSuffix))
            {
                outputFileSuffix = "_Filtered";
            }

            var baseName = Path.GetFileNameWithoutExtension(inputFilePath);
            var filteredFastaFile = new FileInfo(Path.Combine(outputDirectory.FullName, baseName + outputFileSuffix + ".fasta"));

            StreamWriter matchInfoWriter = null;
            if (Options.VerboseMode)
            {
                var matchInfoFilePath = Path.Combine(outputDirectory.FullName, baseName + outputFileSuffix + "_MatchInfo.txt");
                matchInfoWriter = new StreamWriter(new FileStream(matchInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));
                matchInfoWriter.WriteLine("Protein\tFilterMatch\tRegEx");
            }

            Console.WriteLine();
            ShowMessage("Creating the filtered FASTA file:\n  " + GetCompactPath(filteredFastaFile, 100));

            using var writer = new StreamWriter(new FileStream(filteredFastaFile.FullName, FileMode.Create, FileAccess.Write, FileShare.Read));

            while (reader.ReadNextProteinEntry())
            {
                bool keepProtein;
                proteinsRead++;

                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if (filterMode == FilterModes.TaxonomyID)
                {
                    var taxonomyId = ExtractTaxonomyID(reader.ProteinDescription);
                    keepProtein = IsTaxonomyMatch(reader, taxonomyId, taxonomyIDs, matchInfoWriter);
                }
                else
                {
                    var species = ExtractSpecies(reader.ProteinDescription, out _);
                    keepProtein = IsOrganismOrSpeciesMatch(reader, species, organismNameFilters, regExFilters, matchInfoWriter);
                }

                if (keepProtein)
                {
                    WriteFastaFileEntry(writer, reader);
                    proteinsWritten++;
                }

                if (DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds < 10d)
                    continue;

                lastProgressTime = DateTime.UtcNow;

                ShowOptionalBar(true);
                ReportProgress(string.Format("Working: {0:F2}% complete; {1:N0} proteins matched", reader.PercentFileProcessed(), proteinsWritten));
                ShowOptionalBar(false);
            }

            matchInfoWriter?.Close();

            Console.WriteLine();
            ShowMessage("Processing complete: wrote {0:N0} / {1:N0} protein{2} to {3}",
                proteinsWritten, proteinsRead, Pluralize(proteinsRead), filteredFastaFile.Name);

            return true;
        }

        private static bool IsExactOrRegexMatch(
            string proteinName,
            string textToSearch,
            ICollection<string> itemsToMatchExactly,
            Dictionary<string, Regex> regExFilters,
            bool showMessages,
            TextWriter matchInfoWriter)
        {
            if (itemsToMatchExactly.Contains(textToSearch))
            {
                if (showMessages)
                {
                    ShowMessage("Protein {0,-15} matched '{1}'", proteinName, textToSearch);
                }

                matchInfoWriter?.WriteLine(proteinName + '\t' + textToSearch);
                return true;
            }

            foreach (var regExSpec in regExFilters)
            {
                var match = regExSpec.Value.Match(textToSearch);
                if (!match.Success)
                    continue;

                if (showMessages)
                {
                    var contextIndexStart = match.Index - 5;
                    var contextIndexEnd = match.Index + match.Value.Length + 10;

                    if (contextIndexStart < 0)
                        contextIndexStart = 0;

                    if (contextIndexEnd >= textToSearch.Length)
                        contextIndexEnd = textToSearch.Length - 1;

                    ShowMessage("Protein {0,-15} matched '{1}' in '{2}'",
                        proteinName, match.Value,
                        textToSearch.Substring(contextIndexStart, contextIndexEnd - contextIndexStart));
                }

                matchInfoWriter?.WriteLine(proteinName + '\t' + match.Value + '\t' + regExSpec.Key);
                return true;
            }

            return false;
        }

        private bool IsOrganismOrSpeciesMatch(
            ProteinFileReaderBaseClass reader,
            string species,
            ICollection<string> organismNameFilters,
            Dictionary<string, Regex> regExFilters,
            TextWriter matchInfoWriter)
        {
            if (!string.IsNullOrEmpty(species))
            {
                // UniProt FASTA file with OS= entries
                return IsExactOrRegexMatch(reader.ProteinName, species, organismNameFilters, regExFilters, Options.VerboseMode, matchInfoWriter);
            }

            var organism = ExtractOrganism(reader.ProteinDescription);

            if (!string.IsNullOrEmpty(organism))
            {
                // Match organism name within square brackets
                var keepProtein = IsExactOrRegexMatch(reader.ProteinName, organism, organismNameFilters, regExFilters, Options.VerboseMode, matchInfoWriter);
                if (keepProtein)
                    return true;
            }

            // Match the entire protein description
            return IsExactOrRegexMatch(reader.ProteinName, reader.ProteinDescription, organismNameFilters, regExFilters, Options.VerboseMode, matchInfoWriter);
        }

        private bool IsTaxonomyMatch(
            ProteinFileReaderBaseClass reader,
            int taxonomyId,
            ICollection<int> taxonomyIDs,
            TextWriter matchInfoWriter)
        {
            if (!taxonomyIDs.Contains(taxonomyId))
                return false;

            if (Options.VerboseMode)
            {
                ShowMessage("Protein {0,-15} matched taxonomy ID {1}", reader.ProteinName, taxonomyId);
            }

            matchInfoWriter?.WriteLine(reader.ProteinName + '\t' + taxonomyId);
            return true;
        }

        /// <summary>
        /// Filter the FASTA file using the protein names in the protein list file
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="proteinListFilePath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <returns>True if successful, false if an error</returns>
        public bool FilterFastaByProteinName(string inputFilePath, string proteinListFilePath, string outputDirectoryPath)
        {
            try
            {
                Console.WriteLine();

                if (!ValidateInputAndOutputDirectories(inputFilePath, ref outputDirectoryPath, out var outputDirectory))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(proteinListFilePath))
                {
                    ConsoleMsgUtils.ShowError("Protein list file not defined");
                    return false;
                }

                var proteinListFile = new FileInfo(proteinListFilePath);
                if (!proteinListFile.Exists)
                {
                    ConsoleMsgUtils.ShowError("Protein list file not found: " + proteinListFile.FullName);
                    return false;
                }

                ShowMessage("Loading the protein name filters from " + proteinListFile.Name);

                if (!ReadNameFilterFile(proteinListFile, FilterModes.ProteinName, out var proteinNameFilters, out var regExFilters, out _))
                {
                    return false;
                }

                if (proteinNameFilters.Count == 0 && regExFilters.Count == 0)
                {
                    ConsoleMsgUtils.ShowError("Protein list file is empty: " + proteinListFile.FullName);
                    return false;
                }

                var success = FilterFastaByProteinWork(inputFilePath, outputDirectory, proteinNameFilters, regExFilters);

                return success;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error in FilterFastaByProteinName", ex);
                return false;
            }
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private bool FilterFastaByProteinWork(
            string inputFilePath,
            DirectoryInfo outputDirectory,
            ICollection<string> proteinNameFilters,
            Dictionary<string, Regex> regExFilters,
            string outputFileSuffix = "")
        {
            Console.WriteLine();

            var reader = new FastaFileReader();

            if (!reader.OpenFile(inputFilePath))
            {
                ConsoleMsgUtils.ShowError("Error opening the FASTA file; aborting");
                return false;
            }

            var lastProgressTime = DateTime.UtcNow;
            var proteinsRead = 0;
            var proteinsWritten = 0;

            ShowMessage("Parsing " + Path.GetFileName(inputFilePath));
            ShowMessage("Filtering by " + GetFilterModeDescription(FilterModes.ProteinName));

            if (string.IsNullOrWhiteSpace(outputFileSuffix))
            {
                outputFileSuffix = "_Filtered";
            }

            var baseName = Path.GetFileNameWithoutExtension(inputFilePath);
            var filteredFastaFile = new FileInfo(Path.Combine(outputDirectory.FullName, baseName + outputFileSuffix + ".fasta"));

            StreamWriter matchInfoWriter = null;
            if (Options.VerboseMode)
            {
                var matchInfoFilePath = Path.Combine(outputDirectory.FullName, baseName + outputFileSuffix + "_MatchInfo.txt");
                matchInfoWriter = new StreamWriter(new FileStream(matchInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));
                matchInfoWriter.WriteLine("Protein\tFilterMatch\tRegEx");
            }

            Console.WriteLine();
            ShowMessage("Creating the filtered FASTA file:\n  " + GetCompactPath(filteredFastaFile, 100));

            using var writer = new StreamWriter(new FileStream(filteredFastaFile.FullName, FileMode.Create, FileAccess.Write, FileShare.Read));

            while (reader.ReadNextProteinEntry())
            {
                proteinsRead++;

                var keepProtein = IsExactOrRegexMatch(reader.ProteinName, reader.ProteinName, proteinNameFilters, regExFilters, Options.VerboseMode, matchInfoWriter);

                bool keepProteinFromDescription;
                if (!keepProtein && Options.SearchProteinDescriptions)
                {
                    keepProteinFromDescription = IsExactOrRegexMatch(reader.ProteinName, reader.ProteinDescription, proteinNameFilters, regExFilters, Options.VerboseMode, matchInfoWriter);
                }
                else
                {
                    keepProteinFromDescription = false;
                }

                if (keepProtein || keepProteinFromDescription)
                {
                    WriteFastaFileEntry(writer, reader);
                    proteinsWritten++;
                }

                if (DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds < 10d)
                    continue;

                lastProgressTime = DateTime.UtcNow;

                ShowOptionalBar(true);
                ReportProgress(string.Format("Working: {0:F2}% complete; {1:N0} proteins matched", reader.PercentFileProcessed(), proteinsWritten));
                ShowOptionalBar(false);
            }

            matchInfoWriter?.Close();

            Console.WriteLine();
            ShowMessage("Processing complete: wrote {0:N0} / {1:N0} protein{2} to {3}",
                proteinsWritten, proteinsRead, Pluralize(proteinsRead), filteredFastaFile.Name);

            return true;
        }

        /// <summary>
        /// Determine the organism names in a FASTA file
        /// </summary>
        /// <param name="inputFilePath"></param>
        /// <param name="outputDirectoryPath"></param>
        /// <returns>True if successful, false if an error</returns>
        public bool FindOrganismsInFasta(string inputFilePath, string outputDirectoryPath)
        {
            try
            {
                Console.WriteLine();

                var reader = new FastaFileReader
                {
                    DiscardProteinResidues = true
                };

                if (!ValidateInputAndOutputDirectories(inputFilePath, ref outputDirectoryPath, out var outputDirectory))
                {
                    return false;
                }

                var lastProgressTime = DateTime.UtcNow;

                // Key is organism name, value is organism info class
                var organismInfo = new Dictionary<string, OrganismInfo>();

                if (!reader.OpenFile(inputFilePath))
                {
                    ConsoleMsgUtils.ShowError("Error opening the FASTA file; aborting");
                    return false;
                }

                ShowMessage("Parsing " + Path.GetFileName(inputFilePath));
                ShowMessage("Searching for organism names");

                var baseName = Path.GetFileNameWithoutExtension(inputFilePath);

                var mapFile = new FileInfo(Path.Combine(outputDirectory.FullName, baseName + "_ProteinOrganismMap.txt"));

                StreamWriter mapFileWriter = null;
                if (Options.CreateProteinToOrganismMapFile)
                {
                    Console.WriteLine();
                    ShowMessage("Creating the protein to organism map file:\n  " + GetCompactPath(mapFile, 100));

                    mapFileWriter = new StreamWriter(new FileStream(mapFile.FullName, FileMode.Create, FileAccess.Write, FileShare.Read));
                    mapFileWriter.WriteLine("Protein\tOrganism\tTaxonomyID");
                }

                while (reader.ReadNextProteinEntry())
                {
                    var organism = ExtractSpecies(reader.ProteinDescription, out var taxonomyID);

                    if (string.IsNullOrEmpty(organism))
                    {
                        organism = ExtractOrganism(reader.ProteinDescription);
                    }

                    if (string.IsNullOrWhiteSpace(organism))
                    {
                        ShowMessage(" Warning: Organism not found for " + reader.ProteinName);
                        continue;
                    }

                    if (organismInfo.TryGetValue(organism, out var organismStats))
                    {
                        organismStats.ObservationCount++;
                    }
                    else
                    {
                        var newOrganism = new OrganismInfo(organism, taxonomyID) { ObservationCount = 1 };
                        organismInfo.Add(organism, newOrganism);
                    }

                    mapFileWriter?.WriteLine(reader.ProteinName + '\t' + organism + '\t' + taxonomyID);

                    if (DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds < 10)
                        continue;

                    lastProgressTime = DateTime.UtcNow;

                    ShowOptionalBar(true);
                    ReportProgress(string.Format("Working: {0:F1}% complete", reader.PercentFileProcessed()));
                    ShowOptionalBar(false);
                }

                mapFileWriter?.Close();

                var organismSummaryFile = new FileInfo(Path.Combine(outputDirectory.FullName, baseName + "_OrganismSummary.txt"));

                Console.WriteLine();
                ShowMessage("Creating the Organism Summary file:\n  " + GetCompactPath(organismSummaryFile, 100));

                // Now write out the unique list of organisms
                using var summaryWriter = new StreamWriter(new FileStream(organismSummaryFile.FullName, FileMode.Create, FileAccess.Write, FileShare.Read));

                summaryWriter.WriteLine("Organism\tTaxonomyID\tProteins\tGenus\tSpecies");

                var organismsSorted = from item in organismInfo select item;
                var squareBrackets = new[] { '[', ']' };
                var dataToWrite = new List<string>();

                foreach (var organism in organismsSorted)
                {
                    var genus = string.Empty;
                    var species = string.Empty;
                    var nameParts = organism.Key.Split(' ').ToList();
                    if (nameParts.Count > 0)
                    {
                        genus = nameParts[0].Trim(squareBrackets);
                        if (nameParts.Count > 1)
                        {
                            species = nameParts[1];
                        }
                    }

                    dataToWrite.Clear();
                    dataToWrite.Add(organism.Key);
                    dataToWrite.Add(organism.Value.TaxonomyID.ToString());
                    dataToWrite.Add(organism.Value.ObservationCount.ToString());
                    dataToWrite.Add(genus);
                    dataToWrite.Add(species);

                    summaryWriter.WriteLine(string.Join("\t", dataToWrite));
                }

                Console.WriteLine();
                ShowMessage("Processing complete: found {0} organism{1}", organismInfo.Count, Pluralize(organismInfo.Count));
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error in FindOrganismsInFasta", ex);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Shorten the full path to the file to the given length using PathUtils.CompactPathString
        /// If the shortened path does not contain the full filename, update the shortened path to include it
        /// </summary>
        /// <param name="filteredFastaFile"></param>
        /// <param name="maxLength"></param>
        private string GetCompactPath(FileSystemInfo filteredFastaFile, int maxLength)
        {
            var compactPath = PathUtils.CompactPathString(filteredFastaFile.FullName, maxLength);
            if (compactPath.EndsWith(filteredFastaFile.Name))
                return compactPath;

            var lastSlash = compactPath.LastIndexOf(Path.DirectorySeparatorChar);
            if (lastSlash < 0)
                return compactPath;

            var directoryPath = compactPath.Substring(0, lastSlash);
            return Path.Combine(directoryPath, filteredFastaFile.Name);
        }

        /// <summary>
        /// Return s if the count is not 1
        /// </summary>
        /// <param name="count"></param>
        /// <returns>Either s or an empty string</returns>
        private static string Pluralize(int count)
        {
            return count == 1 ? string.Empty : "s";
        }

        private static string GetFilterModeDescription(FilterModes filterMode)
        {
            return filterMode switch
            {
                FilterModes.OrganismName => "organism name",
                FilterModes.ProteinName => "protein name",
                FilterModes.TaxonomyID => "taxonomy ID",
                _ => "??"
            };
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        /// <summary>
        /// Read organism names, protein names or taxonomy IDs from the given text file
        /// </summary>
        /// <param name="nameListFile"></param>
        /// <param name="filterMode">Indicates the current filter mode</param>
        /// <param name="organismOrProteinNameFilters">Sorted set of organism names</param>
        /// <param name="regExFilters">Dictionary where keys are the RegEx text and values are compiled RegEx instances</param>
        /// <param name="taxonomyIDs">Sorted set of taxonomy IDs</param>
        /// <returns>True if successful, false if an error</returns>
        private bool ReadNameFilterFile(
            FileInfo nameListFile,
            FilterModes filterMode,
            out SortedSet<string> organismOrProteinNameFilters,
            out Dictionary<string, Regex> regExFilters,
            out SortedSet<int> taxonomyIDs)
        {
            organismOrProteinNameFilters = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            regExFilters = new Dictionary<string, Regex>();
            taxonomyIDs = new SortedSet<int>();

            var lineNumber = 0;
            try
            {
                using var reader = new StreamReader(new FileStream(nameListFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read));

                while (!reader.EndOfStream)
                {
                    var dataLine = reader.ReadLine();

                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(dataLine))
                        continue;

                    // Look for lines that start with "RegEx:" or "TaxId:"
                    // If no match, look for an integer (indicating Taxonomy ID)
                    // If no match, assume an organism name
                    if (dataLine.StartsWith("RegEx:", StringComparison.OrdinalIgnoreCase))
                    {
                        var regExFilter = dataLine.Substring("RegEx:".Length);
                        if (string.IsNullOrWhiteSpace(regExFilter))
                        {
                            ShowMessage("  Warning: empty RegEx filter defined on line " + lineNumber);
                            continue;
                        }

                        AddRegExExpression(regExFilters, regExFilter);
                    }
                    else if (filterMode != FilterModes.ProteinName && dataLine.StartsWith("TaxId:", StringComparison.OrdinalIgnoreCase))
                    {
                        var taxonomyId = dataLine.Substring("TaxId:".Length);
                        if (string.IsNullOrWhiteSpace(taxonomyId))
                        {
                            ShowMessage("  Warning: empty TaxId defined on line " + lineNumber);
                            continue;
                        }

                        if (int.TryParse(taxonomyId, out var taxId))
                        {
                            taxonomyIDs.Add(taxId);
                        }
                        else
                        {
                            ShowMessage("  Warning: TaxId defined on line {0:N0} is not an integer: {1}", lineNumber, taxonomyId);
                        }
                    }
                    else
                    {
                        if (!organismOrProteinNameFilters.Contains(dataLine))
                        {
                            organismOrProteinNameFilters.Add(dataLine);
                        }
                    }
                }

                var entityDescription = filterMode == FilterModes.ProteinName ? "protein" : "organism";

                if (organismOrProteinNameFilters.Count > 0)
                {
                    ShowMessage("Read {0:N0} {1} name{2} from {3}",
                        organismOrProteinNameFilters.Count, entityDescription, Pluralize(organismOrProteinNameFilters.Count), nameListFile.Name);
                }

                if (regExFilters.Count > 0)
                {
                    ShowMessage("Read {0:N0} {1} name RegEx filter{2} from {3}",
                        regExFilters.Count, entityDescription, Pluralize(regExFilters.Count), nameListFile.Name);
                }

                if (taxonomyIDs.Count > 0)
                {
                    ShowMessage("Read {0:N0} taxonomy ID value{1} from {2}",
                        taxonomyIDs.Count, Pluralize(taxonomyIDs.Count), nameListFile.Name);
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

        private static void ShowMessage(string message)
        {
            Console.WriteLine(message);
        }

        [StringFormatMethod("format")]
        private static void ShowMessage(string format, params object[] arg)
        {
            Console.WriteLine(format, arg);
        }

        private void ShowOptionalBar(bool topBar)
        {
            if (!Options.VerboseMode)
                return;

            if (topBar)
            {
                Console.WriteLine();
                Console.WriteLine("--------------------------------------------");
                return;
            }

            Console.WriteLine("--------------------------------------------");
            Console.WriteLine();
        }

        /// <summary>
        /// Show current processing options
        /// </summary>
        /// <returns></returns>
        public bool ShowCurrentProcessingOptions()
        {
            if (string.IsNullOrWhiteSpace(Options.InputFilePath))
            {
                ConsoleMsgUtils.ShowWarning("Input file not defined; nothing to do");
                return false;
            }

            ShowMessage("{0,-40} {1}", "Input file path:", Options.InputFilePath);

            if (!string.IsNullOrWhiteSpace(Options.OutputDirectoryPath))
            {
                ShowMessage("{0,-40} {1}", "Output directory path:", Options.OutputDirectoryPath);
            }

            if (!string.IsNullOrWhiteSpace(Options.OrganismName))
            {
                ShowMessage("{0,-40} {1}", "Organism name filter:", Options.OrganismName);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(Options.OrganismListFile))
            {
                ShowMessage("{0,-40} {1}", "Organism list file:", Options.OrganismListFile);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(Options.ProteinListFile))
            {
                ShowMessage("{0,-40} {1}", "Protein list file:", Options.ProteinListFile);
                ShowMessage("{0,-40} {1}", "Search protein descriptions:", Options.SearchProteinDescriptions);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(Options.TaxonomyIdListFile))
            {
                ShowMessage("{0,-40} {1}", "Taxonomy ID list file:", Options.TaxonomyIdListFile);
            }

            return true;
        }

        /// <summary>
        /// Process the input file, as defined in Options
        /// </summary>
        /// <returns>True if successful, otherwise false</returns>
        public bool StartProcessing()
        {
            if (string.IsNullOrWhiteSpace(Options.InputFilePath))
            {
                ConsoleMsgUtils.ShowWarning("Input file not defined; nothing to do");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Options.OrganismName))
            {
                return FilterFastaOneOrganism(Options.InputFilePath, Options.OrganismName, Options.OutputDirectoryPath);
            }

            if (!string.IsNullOrWhiteSpace(Options.OrganismListFile))
            {
                return FilterFastaByOrganismName(Options.InputFilePath, Options.OrganismListFile, Options.OutputDirectoryPath);
            }

            if (!string.IsNullOrWhiteSpace(Options.ProteinListFile))
            {
                return FilterFastaByProteinName(Options.InputFilePath, Options.ProteinListFile, Options.OutputDirectoryPath);
            }

            if (!string.IsNullOrWhiteSpace(Options.TaxonomyIdListFile))
            {
                return FilterFastaByTaxonomyID(Options.InputFilePath, Options.TaxonomyIdListFile, Options.OutputDirectoryPath);
            }

            return FindOrganismsInFasta(Options.InputFilePath, Options.OutputDirectoryPath);
        }
        private bool ValidateInputAndOutputDirectories(string inputFilePath, ref string outputDirectoryPath, out DirectoryInfo outputDirectory)
        {
            var sourceFile = new FileInfo(inputFilePath);
            if (!sourceFile.Exists)
            {
                ConsoleMsgUtils.ShowError("Source file not found: " + inputFilePath);
                outputDirectory = null;
                return false;
            }

            if (string.IsNullOrWhiteSpace(outputDirectoryPath) && sourceFile.Directory != null)
            {
                outputDirectoryPath = sourceFile.Directory.FullName;
            }

            outputDirectory = ValidateOutputDirectory(ref outputDirectoryPath);
            if (outputDirectory is null)
                return false;

            if (sourceFile.Directory != null && !outputDirectory.FullName.Equals(sourceFile.Directory.FullName))
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

        private void WriteFastaFileEntry(TextWriter writer, ProteinFileReaderBaseClass reader)
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