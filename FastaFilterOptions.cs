using PRISM;

#pragma warning disable 1591

namespace FastaOrganismFilter
{
    /// <summary>
    /// FASTA organism filter options
    /// </summary>
    public class FastaFilterOptions
    {
        // Ignore Spelling: Desc, Prot

        /// <summary>
        /// Input file path
        /// </summary>
        /// <remarks>.fasta or .fasta.gz file</remarks>
        [Option("InputFilePath", "I",
            ArgPosition = 1, Required = true, HelpShowsDefault = false,
            HelpText = "The name of the FASTA file to process (.fasta or .fasta.gz). " +
                       "Either define this at the command line using /I or in a parameter file. " +
                       "When using /I at the command line, surround the filename with double quotes if it contains spaces")]
        public string InputFilePath { get; set; }

        /// <summary>
        /// Output directory path
        /// </summary>
        [Option("OutputDirectoryPath", "OutputDirectory", "O", HelpShowsDefault = false,
            HelpText = "Output directory name (or full path). " +
                       "If omitted, the output files will be created in the program directory")]
        public string OutputDirectoryPath { get; set; }

        [Option("CreateMapFile", "Map",
            HelpShowsDefault = false,
            HelpText = "Create protein to organism map file. " +
                       "Also creates a file named FastaFileName_OrganismSummary.txt that lists the organism names")]
        public bool CreateProteinToOrganismMapFile { get; set; }

        [Option("OrganismListFile", "Organisms", "Org",
            HelpShowsDefault = false,
            HelpText = "File with organism names to filter on. " +
                       "Lines that start with RegEx: will use the text after the colon for a regular expression-based search. " +
                       "Lines that start with TaxID: will use the number after the colon to match taxonomy ID specified with OX=")]
        public string OrganismListFile { get; set; }

        [Option("OrganismName", "Organism",
            HelpShowsDefault = false,
            HelpText = "Single organism name to filter on")]
        public string OrganismName { get; set; }

        [Option("ProteinListFile", "Proteins", "Prot",
            HelpShowsDefault = false,
            HelpText = "File with protein names to filter on. " +
                       "Lines that start with RegEx: will use the text after the colon for a regular expression-based search.")]
        public string ProteinListFile { get; set; }

        [Option("SearchProteinDescriptions", "SearchDescriptions", "Desc",
            HelpShowsDefault = false,
            HelpText = "When filtering on protein name, also search protein descriptions for the protein names in the ProteinListFile.")]
        public bool SearchProteinDescriptions { get; set; }

        [Option("TaxonomyIdListFile", "TaxonomyIDs", "TaxIDs", "Taxonomy", "Tax",
            HelpShowsDefault = false,
            HelpText = "File with taxonomy IDs to filter on")]
        public string TaxonomyIdListFile { get; set; }

        [Option("VerboseMode", "Verbose",
            HelpShowsDefault = false,
            HelpText = "If provided at the command line (or if set to True in a parameter file), show more status messages")]
        public bool VerboseMode { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public FastaFilterOptions()
        {
            InputFilePath = string.Empty;
            OutputDirectoryPath = string.Empty;

            OrganismName = string.Empty;
            OrganismListFile = string.Empty;
            ProteinListFile = string.Empty;
            TaxonomyIdListFile = string.Empty;
        }

        /// <summary>
        /// Validate the options
        /// </summary>
        /// <remarks>This method is called from Program.cs</remarks>
        /// <returns>True if all options are valid</returns>
        // ReSharper disable once UnusedMember.Global
        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(InputFilePath))
            {
                ConsoleMsgUtils.ShowError($"ERROR: Input path must be provided and non-empty; \"{InputFilePath}\" was provided");
                return false;
            }

            return true;
        }
    }
}
