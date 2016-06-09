@echo off 
echo About to process Uniprot_ArchaeaBacteriaFungi_SprotTrembl_2014_ReductaseSubset_2016-06-09.fasta
echo to create Uniprot_ArchaeaBacteriaFungi_SprotTrembl_2014_ReductaseSubset_2016-06-09_Filtered.fasta
echo and Uniprot_ArchaeaBacteriaFungi_SprotTrembl_2014_ReductaseSubset_2016-06-09_Filtered_MatchInfo.txt

@echo on
..\bin\FastaOrganismFilter.exe Uniprot_ArchaeaBacteriaFungi_SprotTrembl_2014_ReductaseSubset_2016-06-09.fasta /Prot:Uniprot_ArchaeaBacteriaFungi_GenesToFind.txt /Desc /Verbose
pause
