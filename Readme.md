# Fasta Organism Filter

This program reads a FASTA file and tries to determine the organism and/or
taxonomy ID for each protein, searching for standard organism name formats in
protein descriptions. You can optionally filter the proteins to create a new,
filtered FASTA file. The program also supports filtering by protein name.

## Processing Modes

There are 5 processing modes:

### Mode 1

The program will find the organisms present in the FASTA file, creating file
SourceFasta_OrganismSummary.txt. The program first looks for UniProt species tags
`OS=` and `OX=`, for example `OS=Homo Sapiens OX=9606`. If the `OS=` tag is not found, it
looks for the text in the last set of square brackets in the protein description.
If `OS=` is missing and the square brackets are missing, the program searches the
entire description.

Optionally use `/Map` to also create a file mapping protein name to organism name
(filename SourceFasta_ProteinOrganismMap.txt)

### Mode 2

Use `/Org` to specify a text file listing organism names or taxonomy IDs that
should be used for filtering the FASTA file. The program will create a new FASTA
file that only contains proteins from the organisms of interest.

The OrganismListFile should have one organism name per line. Entries that start
with `RegEx:` will be treated as regular expressions to be matched against
organism names. Entries that start with `TaxId:` will be treated as taxonomy IDs
(only applicable if the FASTA file has `OX=` tags). Names or RegEx values are first
matched to UniProt style OS=Organism entries. If no `OS= `entry is present, next
looks for an organism name in square brackets. If no match to a [Organism] tag,
the entire protein description is searched. Taxonomy IDs are matched to `OX=123456`
entries. If not a match, the protein is skipped.

### Mode 3

Use `/Organism` to specify a single organism name to be used for filtering the
FASTA file. The * character is treated as a wildcard. The program will create a
new FASTA file that only contains proteins from that organism.

### Mode 4

Use `/Prot` to filter by protein name, using the proteins listed in the given text
file. The program will create a new FASTA file that only contains the listed
proteins.

The ProteinListFile should have one protein name per line. Protein names that
start with `RegEx:` will be treated as regular expressions for matching to
protein names.

When using Mode 4, optionally use switch `/Desc` to indicate that protein
descriptions should also be searched. By default, the full protein description
must match the names in the protein list file. To match a word inside the
description, use a RegEx filter with wildcards, for example `RegEx:Cytochrome.+`
or `RegEx:.+promoting.+`

### Mode 5

Use `/Tax` to filter by taxonomy ID, using the integers listed in the given text
file. The program will create a new FASTA file that only contains proteins from
the taxonomy IDs of interest.

The TaxonomyIdListFile should have one taxonomy ID per line. Taxonomy IDs are
integers, e.g. 9606 for homo sapiens.

### Output Directory

For all 5 modes, use `/O` to specify an output directory. If /O is missing, the
output files will be created in the same directory as the source file.

## Syntax

Program mode #1:
```
FastaOrganismFilter.exe SourceFile.fasta [/O:OutputDirectoryPath] [/Map]
```

Program mode #2:
```
FastaOrganismFilter.exe SourceFile.fasta /Org:OrganismListFile.txt [/O:OutputDirectoryPath] [/Verbose]
```

Program mode #3:
```
FastaOrganismFilter.exe SourceFile.fasta /Organism:OrganismName [/O:OutputDirectoryPath] [/Verbose]
```

Program mode #4:
```
FastaOrganismFilter.exe SourceFile.fasta /Prot:ProteinListFile.txt [/O:OutputDirectoryPath] [/Desc] [/Verbose]
```

Program mode #5:
```
FastaOrganismFilter.exe SourceFile.fasta /Tax:TaxonomyIdListFile.txt [/O:OutputDirectoryPath] [/Verbose]
```

Use `/Verbose` to see details on each match, including the RegEx expression or 
search keyword that matches a protein name or description.

## Contacts

Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) \
E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov \
Website: https://omics.pnl.gov/ or https://panomics.pnnl.gov/

## License

The FASTA Organism Filter is licensed under the 2-Clause BSD License; 
you may not use this file except in compliance with the License.  You may obtain 
a copy of the License at https://opensource.org/licenses/BSD-2-Clause

Copyright 2014 Battelle Memorial Institute
