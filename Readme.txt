Fasta Organism Splitter

This program reads a fasta file and finds the organism info defined 
in the protein description lines. It optionally creates a filtered 
fasta file containing only the proteins of interest.

Program syntax #1:
FastaOrganismFilter.exe SourceFile.fasta [/O:OutputFolderPath] [/Map]

Program syntax #2:
FastaOrganismFilter.exe SourceFile.fasta /Org:OrganismListFile.txt [/O:OutputFolderPath]

The input file name is required
Surround the filename with double quotes if it contains spaces

For the first syntax, will find the organisms present in the fasta file, 
creating an OrganismSummary file. Assumes the organism name is defined 
by the last set of square brackets in the protein description.

Use /Map to also create a file mapping protein name to organism name
(filename SourceFasta_ProteinOrganismMap.txt)

For the second syntax, use /Org to specify a text file listing organism names 
that should be used for filtering the fasta file. The program will create a 
new fasta file that only contains proteins from the organisms of interest

The OrganismListFile should have one organism name per line
Organism names that start with 'RegEx:' will be treated as regular expressions 
for matching to protein descriptions. Otherwise, assumes that the protein name 
is the text betweeen the last set of square brackets in the protein description

For both modes, use /O to specify an output folder
If /O is missing, the output files will be created in the same folder as the source file

-------------------------------------------------------------------------------
Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
Copyright 2014, Battelle Memorial Institute.  All Rights Reserved.

E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/
-------------------------------------------------------------------------------

Licensed under the Apache License, Version 2.0; you may not use this file except 
in compliance with the License.  You may obtain a copy of the License at 
http://www.apache.org/licenses/LICENSE-2.0

All publications that result from the use of this software should include 
the following acknowledgment statement:
 Portions of this research were supported by the W.R. Wiley Environmental 
 Molecular Science Laboratory, a national scientific user facility sponsored 
 by the U.S. Department of Energy's Office of Biological and Environmental 
 Research and located at PNNL.  PNNL is operated by Battelle Memorial Institute 
 for the U.S. Department of Energy under contract DE-AC05-76RL0 1830.

Notice: This computer software was prepared by Battelle Memorial Institute, 
hereinafter the Contractor, under Contract No. DE-AC05-76RL0 1830 with the 
Department of Energy (DOE).  All rights in the computer software are reserved 
by DOE on behalf of the United States Government and the Contractor as 
provided in the Contract.  NEITHER THE GOVERNMENT NOR THE CONTRACTOR MAKES ANY 
WARRANTY, EXPRESS OR IMPLIED, OR ASSUMES ANY LIABILITY FOR THE USE OF THIS 
SOFTWARE.  This notice including this sentence must appear on any copies of 
this computer software.
