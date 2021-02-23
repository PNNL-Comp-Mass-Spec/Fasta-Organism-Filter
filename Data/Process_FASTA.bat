@echo off

Set ProgramPath=FastaOrganismFilter.exe
If Exist ..\FastaOrganismFilter.exe     Set ProgramPath=..\FastaOrganismFilter.exe
If Exist ..\Bin\FastaOrganismFilter.exe Set ProgramPath=..\Bin\FastaOrganismFilter.exe

echo.
echo -----------------------------------------------------------------
echo Mode 1
echo -----------------------------------------------------------------
@echo on
%ProgramPath% Uniprot_10bacteria.fasta /map
%ProgramPath% Uniprot_10bacteria.fasta /O:results /map
@echo off

echo.
echo -----------------------------------------------------------------
echo Mode 2
echo -----------------------------------------------------------------
@echo on
%ProgramPath% Uniprot_10bacteria.fasta /Org:Bacteria_OrganismsToFind.txt
@echo off

echo.
echo -----------------------------------------------------------------
echo Mode 3
echo -----------------------------------------------------------------
@echo on
%ProgramPath% Uniprot_10bacteria.fasta /Organism:"Escherichia coli*"
@echo off

echo.
echo -----------------------------------------------------------------
echo Mode 4
echo -----------------------------------------------------------------
@echo on
%ProgramPath% Uniprot_10bacteria.fasta /Prot:Bacteria_ProteinsToFind.txt
%ProgramPath% Uniprot_10bacteria.fasta /Prot:Bacteria_ProteinsToFind_RegEx.txt
%ProgramPath% Uniprot_10bacteria.fasta /Prot:Bacteria_TextToFindInDescription.txt /Desc
@echo off

echo.
echo -----------------------------------------------------------------
echo Mode 5
echo -----------------------------------------------------------------
@echo on
%ProgramPath% Uniprot_10bacteria.fasta /tax:Bacteria_TaxonomyIDsToFind.txt
@echo off

pause
