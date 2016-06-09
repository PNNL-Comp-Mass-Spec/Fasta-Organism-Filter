== Fasta Organism Filter ==

This program reads a FASTA file and filters the proteins 
by either organism name or protein name.

Program modes 1 through 3
- Filter by organism name, as listed in the protein description
   - Run the program without /Org or /Organism to create a text file listing the organisms present
   - Optionally include /Map to create a file mapping protein name to organism
   - Use the /Org switch to filter with an organism list file
	   - Either list exact organism names, or use flag RegEx: as mentioned below
   - Use the /Organism switch to filter by organism name

Program mode 4
- Filter by protein name, as listed in a text file
   - Use the /Prot switch with a protein list file
   - Either list exact protein names, or use flag RegEx: as mentioned below

=== Details ===

Program mode #1:
FastaOrganismFilter.exe SourceFile.fasta [/O:OutputFolderPath] [/Map] [/Verbose]

Program mode #2:
FastaOrganismFilter.exe SourceFile.fasta /Org:OrganismListFile.txt [/O:OutputFolderPath] [/Verbose]

Program mode #3:
FastaOrganismFilter.exe SourceFile.fasta /Organism:OrganismName [/O:OutputFolderPath] [/Verbose]

Program mode #4:
FastaOrganismFilter.exe SourceFile.fasta /Prot:ProteinListFile.txt [/O:OutputFolderPath] [/Desc] [/Verbose]


The input file name is required
Surround the filename with double quotes if it contains spaces

Mode 1: will find the organisms present in the fasta file,
creating an OrganismSummary file. First looks for a Uniprot sequence tag,
for example OS=Homo Sapiens.  If that tag is not found, then looks for the
name in the last set of square brackets in the protein description.
If OS= is missing and the square brackets are missing, searches the
entire description.

Use /Map to also create a file mapping protein name to organism name
(filename SourceFasta_ProteinOrganismMap.txt

Mode 2: use /Org to specify a text file listing organism names
that should be used for filtering the fasta file. The program will create a
new fasta file that only contains proteins from the organisms of interest

The OrganismListFile should have one organism name per line
Entries that start with 'RegEx:' will be treated as regular expressions.
Names or RegEx values are first matched to Uniprot style OS=Organism entries
If not a match, the protein is skipped. If no OS= entry is present, next looks
for an organism name in square brackets. If no match to a [Organism] tag,
the entire protein description is searched.

Mode 3: use /Organism to specify a single organism name
to be used for filtering the fasta file. The * character is treated as a wildcard.
The program will create a new fasta file that only contains proteins from that
organism.

Mode 4: use /Prot to filter by protein name, using the proteins listed in the given text file.
The program will create a new fasta file that only contains the listed proteins.

The ProteinListFile should have one protein name per line
Protein names that start with 'RegEx:' will be treated as regular expressions
for matching to protein names.

When using Mode 4, optionally use switch /Desc to indicate that protein descriptions should also be searched


For all 4 modes, use /O to specify an output folder
If /O is missing, the output files will be created in the same folder as the source file

Use /Verbose to see details on each match, including the RegEx expression or 
search keyword that matches a protein name or description.

=== Filtering by Protein Name ===

Create a text file with a list of protein names to retrieve, then run this program 
with the /Prot switch.  Note that you can use the Protein Digestion Simulator to
convert the fasta file to a tab-delimited text file listing the protein names 
and descriptions.  That program also has the option of excluding the 
protein sequences from the output file (to reduce file size).

-------------------------------------------------------------------------------
Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
Copyright 2014, Battelle Memorial Institute.  All Rights Reserved.

E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/
-------------------------------------------------------------------------------

Licensed under the Apache License, Version 2.0; you may not use this file except 
in compliance with the License.  You may obtain a copy of the License at 
http://www.apache.org/licenses/LICENSE-2.0
