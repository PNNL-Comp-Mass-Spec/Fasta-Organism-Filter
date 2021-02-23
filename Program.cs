using System;
using System.IO;
using System.Text;
using PRISM;

namespace FastaOrganismFilter
{
    /// <summary>
    /// This program reads a FASTA file and finds the organism info defined in the protein description lines
    /// It optionally creates a filtered FASTA file containing only the proteins of interest
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
    /// Program started in 2014
    /// </para>
    /// <para>
    /// E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov
    /// Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/
    /// </para>
    /// </remarks>
    internal static class Program
    {
        public const string PROGRAM_DATE = "February 22, 2021";

        // Ignore Spelling: Prot, Desc, UniProt, Conf

        public static int Main(string[] args)
        {
            var programName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
            var exePath = PRISM.FileProcessor.ProcessFilesOrDirectoriesBase.GetAppPath();
            var exeName = Path.GetFileName(exePath);
            var cmdLineParser = new CommandLineParser<FastaFilterOptions>(programName, GetAppVersion())
            {
                ProgramInfo = GetProgramInfo(),
                ContactInfo = "Program written by Matthew Monroe for PNNL (Richland, WA) in 2010" + Environment.NewLine +
                              "E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov" + Environment.NewLine +
                              "Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/"
            };

            cmdLineParser.UsageExamples.Add("Program syntax:" + Environment.NewLine + exeName + "\n" +
                                            " /I:InputFileNameOrDirectoryPath [/O:OutputDirectoryPath] [/Map]\n" +
                                            " [/Org:OrganismListFile.txt]\n" +
                                            " [/Organism:OrganismName]\n" +
                                            " [/Prot:ProteinListFile.txt] [/Desc]\n" +
                                            " [/Tax:TaxonomyIdListFile.txt]\n" +
                                            " [/Verbose]");

            cmdLineParser.UsageExamples.Add("Program mode #1:\n" + exeName + " SourceFile.fasta [/O:OutputDirectoryPath] [/Map]");
            cmdLineParser.UsageExamples.Add("Program mode #2:\n" + exeName + " SourceFile.fasta /Org:OrganismListFile.txt [/O:OutputDirectoryPath] [/Verbose]");
            cmdLineParser.UsageExamples.Add("Program mode #3:\n" + exeName + " SourceFile.fasta /Organism:OrganismName [/O:OutputDirectoryPath]");
            cmdLineParser.UsageExamples.Add("Program mode #4:\n" + exeName + " SourceFile.fasta /Prot:ProteinListFile.txt [/O:OutputDirectoryPath] [/Desc]");
            cmdLineParser.UsageExamples.Add("Program mode #5:\n" + exeName + " SourceFile.fasta /Tax:TaxonomyIdListFile.txt [/O:OutputDirectoryPath]");

            // The default argument name for parameter files is /ParamFile or -ParamFile
            // Also allow /Conf or /P
            cmdLineParser.AddParamFileKey("Conf");
            cmdLineParser.AddParamFileKey("P");

            var result = cmdLineParser.ParseArgs(args);
            var options = result.ParsedResults;
            if (!result.Success || !options.Validate())
            {
                // Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
                System.Threading.Thread.Sleep(750);
                return -1;
            }

            try
            {
                var organismFilter = new FilterFastaByOrganism(options);

                organismFilter.ShowCurrentProcessingOptions();

                var success = organismFilter.StartProcessing();

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
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + " (" + PROGRAM_DATE + ")";
        }

        private static string GetProgramInfo()
        {
            try
            {
                var programInfo = new StringBuilder();

                programInfo.AppendLine(ConsoleMsgUtils.WrapParagraph(
                    "This program reads a FASTA file and tries to determine the organism and/or taxonomy ID for each protein, " +
                    "searching for standard organism name formats in protein descriptions. " +
                    "You can optionally filter the proteins to create a new, filtered FASTA file. " +
                    "The program also supports filtering by protein name."));

                programInfo.AppendLine();
                programInfo.AppendLine("There are 5 processing modes:");

                programInfo.AppendLine();
                programInfo.AppendLine("== Mode 1 ==");
                programInfo.AppendLine();
                programInfo.AppendLine(ConsoleMsgUtils.WrapParagraph(
                    "The program will find the organisms present in the FASTA file, creating file SourceFasta_OrganismSummary.txt. " +
                    "The program first looks for UniProt species tags OS= and OX=, for example OS=Homo Sapiens OX=9606. " +
                    "If the OS= tag is not found, it looks for the text in the last set of square brackets in the protein description. " +
                    "If OS= is missing and the square brackets are missing, the program searches the entire description."));

                programInfo.AppendLine();
                programInfo.AppendLine(ConsoleMsgUtils.WrapParagraph(
                    "Optionally use /Map to also create a file mapping protein name to organism name\n" +
                    "(filename SourceFasta_ProteinOrganismMap.txt)"));

                programInfo.AppendLine();
                programInfo.AppendLine("== Mode 2 ==");
                programInfo.AppendLine();
                programInfo.AppendLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /Org to specify a text file listing organism names or taxonomy IDs that should be used for filtering the FASTA file. " +
                    "The program will create a new FASTA file that only contains proteins from the organisms of interest."));

                programInfo.AppendLine();
                programInfo.AppendLine(ConsoleMsgUtils.WrapParagraph(
                    "The OrganismListFile should have one organism name per line. " +
                    "Entries that start with 'RegEx:' will be treated as regular expressions to be matched against organism names. " +
                    "Entries that start with 'TaxId:' will be treated as taxonomy IDs (only applicable if the FASTA file has OX= tags). " +
                    "Names or RegEx values are first matched to UniProt style OS=Organism entries. " +
                    "If no OS= entry is present, next looks for an organism name in square brackets. " +
                    "If no match to a [Organism] tag, the entire protein description is searched. " +
                    "Taxonomy IDs are matched to OX=123456 entries. " +
                    "If not a match, the protein is skipped."));

                programInfo.AppendLine();
                programInfo.AppendLine("== Mode 3 ==");
                programInfo.AppendLine();
                programInfo.AppendLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /Organism to specify a single organism name to be used for filtering the FASTA file. " +
                    "The * character is treated as a wildcard. " +
                    "The program will create a new FASTA file that only contains proteins from that organism."));

                programInfo.AppendLine();
                programInfo.AppendLine("== Mode 4 ==");
                programInfo.AppendLine();
                programInfo.AppendLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /Prot to filter by protein name, using the proteins listed in the given text file. " +
                    "The program will create a new FASTA file that only contains the listed proteins."));

                programInfo.AppendLine();
                programInfo.AppendLine(ConsoleMsgUtils.WrapParagraph(
                    "The ProteinListFile should have one protein name per line. " +
                    "Protein names that start with 'RegEx:' will be treated as regular expressions for matching to protein names."));
                programInfo.AppendLine();
                programInfo.AppendLine(ConsoleMsgUtils.WrapParagraph(
                    "When using Mode 4, optionally use switch /Desc to indicate that protein descriptions should also be searched. " +
                    "By default, the full protein description must match the names in the protein list file. " +
                    "To match a word inside the description, use a RegEx filter with wildcards, for example 'RegEx:Cytochrome.+' or 'RegEx:.+promoting.+'"));

                programInfo.AppendLine();
                programInfo.AppendLine("== Mode 5 ==");
                programInfo.AppendLine();
                programInfo.AppendLine(ConsoleMsgUtils.WrapParagraph(
                    "Use /Tax to filter by taxonomy ID, using the integers listed in the given text file. " +
                    "The program will create a new FASTA file that only contains proteins from the taxonomy IDs of interest."));

                programInfo.AppendLine();
                programInfo.AppendLine(ConsoleMsgUtils.WrapParagraph(
                    "The TaxonomyIdListFile should have one taxonomy ID per line. Taxonomy IDs are integers, e.g. 9606 for homo sapiens."));

                programInfo.AppendLine();
                programInfo.AppendLine(ConsoleMsgUtils.WrapParagraph(
                    "For all 5 modes, use /O to specify an output directory. " +
                    "If /O is missing, the output files will be created in the same directory as the source file."));

                return programInfo.ToString();
            }
            catch (Exception ex)
            {
                ConsoleMsgUtils.ShowError("Error generating the program syntax", ex);
                return string.Empty;
            }
        }
    }
}