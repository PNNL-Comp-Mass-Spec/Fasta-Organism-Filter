== Fasta Organism Filter ==

This program reads a FASTA file and filters the proteins 
by either organism name or protein name.

Mode A
- Filter by organism name, as listed in the protein description
   - Run the program without /Org or /Organism to create a text file listing the organisms present
   - Optionally include /Map to create a file mapping protein name to organism
   - Use the /Org switch to filter with an organism list file
   - Use the /Organism switch to filter by organism name

Mode B
- Filter by protein name, as listed in a text file
   - Use the /Prot list with a protein list file

=== Filtering by Organism ===

Program syntax #1:
FastaOrganismFilter.exe SourceFile.fasta [/O:OutputFolderPath] [/Map]

Program syntax #2:
FastaOrganismFilter.exe SourceFile.fasta /Org:OrganismListFile.txt [/O:OutputFolderPath]

Program syntax #3:
FastaOrganismFilter.exe SourceFile.fasta /Organism:OrganismName [/O:OutputFolderPath]

The input file name is required
Surround the filename with double quotes if it contains spaces

Syntax 1: will find the organisms present in the fasta file, 
creating an OrganismSummary file. Assumes the organism name is defined 
by the last set of square brackets in the protein description.

Use /Map to also create a file mapping protein name to organism name
(filename SourceFasta_ProteinOrganismMap.txt

Syntax 2: use /Org to specify a text file listing organism names 
that should be used for filtering the fasta file. The program will create a 
new fasta file that only contains proteins from the organisms of interest

The OrganismListFile should have one organism name per line
Organism names that start with 'RegEx:' will be treated as regular expressions 
for matching to protein descriptions. Otherwise, assumes that the protein name 
is the text betweeen the last set of square brackets in the protein description

Syntax 3: use /Organism to specify a single organism name 
to be used for filtering the fasta file. The * character is treated as a wildcard. 
The program will create a new fasta file that only contains proteins from that organism

For all 3 modes, use /O to specify an output folder
If /O is missing, the output files will be created in the same folder as the source file


=== Filtering by Protein Name ===

Create a text file with a list of protein names to retrieve, then run this program 
with the /Prot switch.  Note that you can use the Protein Digestion Simulator to
convert the fasta file to a tab-delimited text file listing the protein names 
and descriptions.  That program also has the option of excluding the 
protein sequences from the output file (to reduce file size).

Program syntax #4:
FastaOrganismFilter.exe SourceFile.fasta /Prot:ProteinListFile.txt [/O:OutputFolderPath]


-------------------------------------------------------------------------------
Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
Copyright 2014, Battelle Memorial Institute.  All Rights Reserved.

E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/
-------------------------------------------------------------------------------

Licensed under the Apache License, Version 2.0; you may not use this file except 
in compliance with the License.  You may obtain a copy of the License at 
http://www.apache.org/licenses/LICENSE-2.0
