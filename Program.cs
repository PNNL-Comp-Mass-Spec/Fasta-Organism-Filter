// This program reads a fasta file and finds the organism info defined in the protein description lines
// It optionally creates a filtered fasta file containing only the proteins of interest
//
// -------------------------------------------------------------------------------
// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
// Program started September 4, 2014
// Copyright 2014, Battelle Memorial Institute.  All Rights Reserved.

// E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
// Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/ or http://panomics.pnnl.gov/
// -------------------------------------------------------------------------------
//
// Licensed under the Apache License, Version 2.0; you may not use this file except
// in compliance with the License.  You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
using System;
using System.Collections.Generic;

using PRISM;

namespace FastaOrganismFilter
{
    static class Program
    {
        public const string PROGRAM_DATE = "February 16, 2021";
        private static string mInputFilePath;
        private static string mOutputDirectoryPath;
        private static string mOrganismListFile;
        private static string mOrganismName;
        private static string mProteinListFile;
        private static bool mCreateProteinToOrganismMapFile;
        private static bool mSearchProteinDescriptions;
        private static bool mVerboseMode;

        // Ignore Spelling: Prot, Desc, UniProt

        public static int Main()
        {
            try
            {
                // Set the default values
                mInputFilePath = string.Empty;
                mOrganismListFile = string.Empty;
                mOrganismName = string.Empty;
                mProteinListFile = string.Empty;
                var proceed = false;
                var commandLineParser = new clsParseCommandLine();
                if (commandLineParser.ParseCommandLine())
                {
                    if (SetOptionsUsingCommandLineParameters(commandLineParser))
                        proceed = true;
                }

                if (!proceed || commandLineParser.NeedToShowHelp || commandLineParser.ParameterCount + commandLineParser.NonSwitchParameterCount == 0 || mInputFilePath.Length == 0)
                {
                    ShowProgramHelp();
                    return -1;
                }

                var organismFilter = new FilterFastaByOrganism()
                {
                    CreateProteinToOrganismMapFile = mCreateProteinToOrganismMapFile,
                    SearchProteinDescriptions = mSearchProteinDescriptions,
                    VerboseMode = mVerboseMode
                };
                bool success;
                if (!string.IsNullOrEmpty(mOrganismName))
                {
                    success = organismFilter.FilterFastaOneOrganism(mInputFilePath, mOrganismName, mOutputDirectoryPath);
                }
                else if (!string.IsNullOrEmpty(mOrganismListFile))
                {
                    success = organismFilter.FilterFastaByOrganismName(mInputFilePath, mOrganismListFile, mOutputDirectoryPath);
                }
                else if (!string.IsNullOrEmpty(mProteinListFile))
                {
                    success = organismFilter.FilterFastaByProteinName(mInputFilePath, mProteinListFile, mOutputDirectoryPath);
                }
                else
                {
                    success = organismFilter.FindOrganismsInFasta(mInputFilePath, mOutputDirectoryPath);
                }

                if (success)
                {
                    return 0;
                }

                return -1;
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error occurred in Program->Main", ex);
                return -1;
            }
        }

        private static string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + " (" + PROGRAM_DATE + ")";
        }

        private static bool SetOptionsUsingCommandLineParameters(clsParseCommandLine commandLineParser)
        {
            // Returns True if no problems; otherwise, returns false

            var value = string.Empty;
            var validParameters = new List<string>() { "I", "Org", "O", "Map", "Organism", "Prot", "Desc", "Verbose" };
            try
            {
                // Make sure no invalid parameters are present
                if (commandLineParser.InvalidParametersPresent(validParameters))
                {
                    ConsoleMsgUtils.ShowErrors("Invalid command line parameters", commandLineParser.InvalidParameters(validParameters));
                    return false;
                }
                else
                {

                    // Query commandLineParser to see if various parameters are present

                    if (commandLineParser.RetrieveValueForParameter("I", out value))
                    {
                        mInputFilePath = value;
                    }
                    else if (commandLineParser.NonSwitchParameterCount > 0)
                    {
                        mInputFilePath = commandLineParser.RetrieveNonSwitchParameter(0);
                    }

                    if (commandLineParser.RetrieveValueForParameter("Org", out value))
                    {
                        mOrganismListFile = value;
                    }

                    if (commandLineParser.RetrieveValueForParameter("Organism", out value))
                    {
                        mOrganismName = value;
                    }

                    if (commandLineParser.RetrieveValueForParameter("O", out value))
                    {
                        mOutputDirectoryPath = value;
                    }

                    if (commandLineParser.RetrieveValueForParameter("Prot", out value))
                    {
                        mProteinListFile = value;
                    }

                    if (commandLineParser.IsParameterPresent("Map"))
                        mCreateProteinToOrganismMapFile = true;
                    if (commandLineParser.IsParameterPresent("Desc"))
                        mSearchProteinDescriptions = true;
                    if (commandLineParser.IsParameterPresent("Verbose"))
                        mVerboseMode = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error parsing the command line parameters", ex);
            }

            return false;
        }

        private static void ShowProgramHelp()
        {
            try
            {
                var exeName = System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                ConsoleMsgUtils.WrapParagraph("This program reads a FASTA file and filters the proteins " + "by either organism name or protein name. For organism filtering, " + "the organism is determined using the protein description lines. " + "It optionally creates a filtered fasta file containing only the proteins of interest.");
                Console.WriteLine();
                Console.WriteLine("Program mode #1:\n" + exeName + " SourceFile.fasta [/O:OutputDirectoryPath] [/Map] [/Verbose]");
                Console.WriteLine();
                Console.WriteLine("Program mode #2:\n" + exeName + " SourceFile.fasta /Org:OrganismListFile.txt [/O:OutputDirectoryPath] [/Verbose]");
                Console.WriteLine();
                Console.WriteLine("Program mode #3:\n" + exeName + " SourceFile.fasta /Organism:OrganismName [/O:OutputDirectoryPath] [/Verbose]");
                Console.WriteLine();
                Console.WriteLine("Program mode #4:\n" + exeName + " SourceFile.fasta /Prot:ProteinListFile.txt [/O:OutputDirectoryPath] [/Desc] [/Verbose]");
                Console.WriteLine();
                ConsoleMsgUtils.WrapParagraph("The input file name is required. Surround the filename with double quotes if it contains spaces");
                Console.WriteLine();
                ConsoleMsgUtils.WrapParagraph("Mode 1: will find the organisms present in the fasta file, " + "creating an OrganismSummary file. First looks for a UniProt sequence tag, " + "for example OS=Homo Sapiens.  If that tag is not found, then looks for the" + "name in the last set of square brackets in the protein description. " + "If OS= is missing and the square brackets are missing, searches the" + "entire description.");
                Console.WriteLine();
                ConsoleMsgUtils.WrapParagraph("Use /Map to also create a file mapping protein name to organism name (filename SourceFasta_ProteinOrganismMap.txt");
                Console.WriteLine();
                ConsoleMsgUtils.WrapParagraph("Mode 2: use /Org to specify a text file listing organism names " + "that should be used for filtering the fasta file. The program will create a " + "new fasta file that only contains proteins from the organisms of interest");
                Console.WriteLine();
                ConsoleMsgUtils.WrapParagraph("The OrganismListFile should have one organism name per line" + "Entries that start with 'RegEx:' will be treated as regular expressions." + "Names or RegEx values are first matched to UniProt style OS=Organism entries" + "If not a match, the protein is skipped. If no OS= entry is present, next looks" + "for an organism name in square brackets. If no match to a [Organism] tag," + "the entire protein description is searched.");
                Console.WriteLine();
                ConsoleMsgUtils.WrapParagraph("Mode 3: use /Organism to specify a single organism name" + "to be used for filtering the fasta file. The * character is treated as a wildcard. " + "The program will create a new fasta file that only contains proteins from that organism.");
                Console.WriteLine();
                ConsoleMsgUtils.WrapParagraph(@"Mode 4: use /Prot to filter by protein name, using the proteins listed in the given text file.
                The program will create a new fasta file that only contains the listed proteins.");
                Console.WriteLine();
                ConsoleMsgUtils.WrapParagraph("The ProteinListFile should have one protein name per line. " + "Protein names that start with 'RegEx:' will be treated as regular expressions for matching to protein names.");
                Console.WriteLine();
                ConsoleMsgUtils.WrapParagraph("When using Mode 4, optionally use switch /Desc to indicate that protein descriptions should also be searched");
                Console.WriteLine();
                ConsoleMsgUtils.WrapParagraph("For all 4 modes, use /O to specify an output directory. " + "If /O is missing, the output files will be created in the same directory as the source file");
                Console.WriteLine();
                ConsoleMsgUtils.WrapParagraph("Use /Verbose to see details on each match, including the RegEx expression or search keyword that matches a protein name or description");
                Console.WriteLine();
                ConsoleMsgUtils.WrapParagraph("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2010");
                Console.WriteLine("Version: " + GetAppVersion());
                Console.WriteLine();
                Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov");
                Console.WriteLine("Website: https://omics.pnl.gov/ or https://github.com/pnnl-comp-mass-spec");
                Console.WriteLine();

                // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                System.Threading.Thread.Sleep(750);
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error displaying the program syntax", ex);
            }
        }
    }
}