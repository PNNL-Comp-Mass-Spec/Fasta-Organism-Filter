Option Strict On

' This class reads a fasta file and finds the organism info defined in the protein description lines
' It optionally creates a filtered fasta file containing only the proteins of interest
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Program started September 4, 2014
' Copyright 2014, Battelle Memorial Institute.  All Rights Reserved.

' E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
' Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/
' -------------------------------------------------------------------------------
'
' Licensed under the Apache License, Version 2.0; you may not use this file except
' in compliance with the License.  You may obtain a copy of the License at 
' http://www.apache.org/licenses/LICENSE-2.0
'
' Notice: This computer software was prepared by Battelle Memorial Institute, 
' hereinafter the Contractor, under Contract No. DE-AC05-76RL0 1830 with the 
' Department of Energy (DOE).  All rights in the computer software are reserved 
' by DOE on behalf of the United States Government and the Contractor as 
' provided in the Contract.  NEITHER THE GOVERNMENT NOR THE CONTRACTOR MAKES ANY 
' WARRANTY, EXPRESS OR IMPLIED, OR ASSUMES ANY LIABILITY FOR THE USE OF THIS 
' SOFTWARE.  This notice including this sentence must appear on any copies of 
' this computer software.
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text.RegularExpressions

Public Class clsFilterFastaByOrganism

#Region "Constants and Enums"

#End Region

#Region "Structures"


#End Region

#Region "Classwide Variables"

	Protected Const MAX_PROTEIN_DESCRIPTION_LENGTH As Integer = 7500
	
#End Region

#Region "Properties"

	Public Property CreateProteinToOrganismMapFile As Boolean
#End Region

	Private Function ExtractOrganism(ByVal proteinDescription As String) As String

		' Search for the organism info, assuming it is the last text seen between two square brackets
		' However, there are exceptions we have to consider, for example
		' [Salmonella enterica subsp. enterica serovar 4,[5],12:i:-]

		Dim indexEnd As Integer = proteinDescription.LastIndexOf("]"c)
		If indexEnd >= 0 Then
			' Back track until we find [
			' But, watch for ] while back-tracking
			Dim subLevel = 1

			For index = indexEnd - 1 To 0 Step -1
				Dim chChar = proteinDescription.Substring(index, 1)
				If chChar = "]" Then
					subLevel += 1
				ElseIf chChar = "[" Then
					subLevel -= 1
					If subLevel = 0 Then
						Dim organism = proteinDescription.Substring(index + 1, indexEnd - index - 1)
						Return organism
					End If
				End If
			Next

			Return proteinDescription.Substring(0, indexEnd - 1)			

		End If

		Return String.Empty

	End Function

    Public Function FilterFastaOneOrganism(ByVal inputFilePath As String, ByVal organismName As String, ByVal outputFolderPath As String) As Boolean

        Try

            Dim diOutputFolder As DirectoryInfo = Nothing
            If Not ValidateInputAndOutputFolders(inputFilePath, outputFolderPath, diOutputFolder) Then
                Return False
            End If

            If String.IsNullOrWhiteSpace(organismName) Then
                ShowErrorMessage("Organism name is empty")
                Return False
            End If

            ' Keys in this dictionary are the organism names to filter on; values are meaningless integers
            ' The reason for using a dictionary is to provide fast lookups, but without case sensitivity
            ' I wanted to use a SortedSet but you can't define it without case sensitivity
            Dim lstTextFilters = New Dictionary(Of String, Integer)
            Dim lstRegExFilters = New SortedSet(Of Regex)

            If organismName.Contains("*") Then
                lstRegExFilters.Add(New Regex(organismName.Replace("*", ".+"), RegexOptions.Compiled Or RegexOptions.IgnoreCase))
            Else
                lstTextFilters.Add(organismName, 0)
            End If


            Dim badChars = New List(Of Char) From {" "c, "\"c, "/"c, ":"c, "*"c, "?"c, "."c, "<"c, ">"c, "|"c}
            Dim outputFileSuffix = "_"
            For Each chCharacter In organismName
                If badChars.Contains(chCharacter) Then
                    outputFileSuffix &= "_"
                Else
                    outputFileSuffix &= chCharacter
                End If
            Next
            outputFileSuffix = outputFileSuffix.TrimEnd("_"c)

            Dim success = FilterFastaByOrganismWork(inputFilePath, diOutputFolder, lstTextFilters, lstRegExFilters, outputFileSuffix)

            Return success

        Catch ex As Exception
            ShowErrorMessage("Error in FindOrganismsInFasta: " & ex.Message)
            Return False
        End Try

    End Function

	Public Function FilterFastaByOrganism(ByVal inputFilePath As String, ByVal organismListFile As String, ByVal outputFolderPath As String) As Boolean

		Try

            Dim diOutputFolder As DirectoryInfo = Nothing
			If Not ValidateInputAndOutputFolders(inputFilePath, outputFolderPath, diOutputFolder) Then
				Return False
			End If

			If String.IsNullOrWhiteSpace(organismListFile) Then
				ShowErrorMessage("Organism list file not defined")
				Return False
			End If

			Dim fiOrganismListFile = New FileInfo(organismListFile)
			If Not fiOrganismListFile.Exists Then
				ShowErrorMessage("Organism list file not found: " & fiOrganismListFile.FullName)
				Return False
			End If

			ShowMessage("Loading the organism name filters from " & fiOrganismListFile.Name)

			' Keys in this dictionary are the organism names to filter on; values are meaningless integers
			' The reason for using a dictionary is to provide fast lookups, but without case sensitivity
			' I wanted to use a SortedSet but you can't define it without case sensitivity
			Dim lstTextFilters As Dictionary(Of String, Integer) = Nothing
			Dim lstRegExFilters As SortedSet(Of Regex) = Nothing

			If Not ReadOrganismFilterFile(fiOrganismListFile, lstTextFilters, lstRegExFilters) Then
				Return False
			End If

			If lstTextFilters.Count = 0 AndAlso lstRegExFilters.Count = 0 Then
				ShowErrorMessage("Organism list file is empty: " & fiOrganismListFile.FullName)
				Return False
			End If

            Dim success = FilterFastaByOrganismWork(inputFilePath, diOutputFolder, lstTextFilters, lstRegExFilters)

            Return success

		Catch ex As Exception
			ShowErrorMessage("Error in FindOrganismsInFasta: " & ex.Message)
			Return False
		End Try

	End Function

    Private Function FilterFastaByOrganismWork(
     ByVal inputFilePath As String,
     ByVal diOutputFolder As DirectoryInfo,
     ByVal lstTextFilters As Dictionary(Of String, Integer),
     ByVal lstRegExFilters As SortedSet(Of Regex)) As Boolean
        Return FilterFastaByOrganismWork(inputFilePath, diOutputFolder, lstTextFilters, lstRegExFilters, "")
    End Function

    Private Function FilterFastaByOrganismWork(
      ByVal inputFilePath As String,
      ByVal diOutputFolder As DirectoryInfo,
      ByVal lstTextFilters As Dictionary(Of String, Integer),
      ByVal lstRegExFilters As SortedSet(Of Regex),
      ByVal outputFileSuffix As String) As Boolean

        Dim oReader = New ProteinFileReader.FastaFileReader()
        Dim intProteinsProcessed = 0

        If Not oReader.OpenFile(inputFilePath) Then
            ShowErrorMessage("Error opening the fasta file; aborting")
            Return False
        End If

        ShowMessage("Parsing " & Path.GetFileName(inputFilePath))

        If String.IsNullOrWhiteSpace(outputFileSuffix) Then
            outputFileSuffix = "_Filtered"
        End If

        Dim baseName = Path.GetFileNameWithoutExtension(inputFilePath)
        Dim filteredFastaFilePath = Path.Combine(diOutputFolder.FullName, baseName & outputFileSuffix & ".fasta")

        ShowMessage("Creating " & Path.GetFileName(filteredFastaFilePath))

        Using swFilteredFasta = New StreamWriter(New FileStream(filteredFastaFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))

            While oReader.ReadNextProteinEntry()

                Dim organism = ExtractOrganism(oReader.ProteinDescription)

                Dim keepProtein = False

                If lstTextFilters.ContainsKey(organism) Then
                    keepProtein = True
                Else
                    For Each regExSpec In lstRegExFilters
                        If regExSpec.IsMatch(oReader.ProteinDescription) Then
                            keepProtein = True
                            Exit For
                        End If
                    Next
                End If

                If keepProtein Then
                    WriteFastaFileEntry(swFilteredFasta, oReader)
                End If

                intProteinsProcessed += 1
                If intProteinsProcessed Mod 50000 = 0 Then
                    ReportProgress("Working: " & oReader.PercentFileProcessed & "% fasta")
                End If

            End While

        End Using

        Return True

    End Function

	Public Function FindOrganismsInFasta(ByVal inputFilePath As String, ByVal outputFolderPath As String) As Boolean

		Try

			Dim oReader = New ProteinFileReader.FastaFileReader()
			Dim intProteinsProcessed = 0

			Dim diOutputFolder As DirectoryInfo = Nothing
			If Not ValidateInputAndOutputFolders(inputFilePath, outputFolderPath, diOutputFolder) Then
				Return False
			End If

			' Key is organism name, value is protein usage count			
			Dim lstOrganisms = New Dictionary(Of String, Integer)

			If Not oReader.OpenFile(inputFilePath) Then
				ShowErrorMessage("Error opening the fasta file; aborting")
				Return False
			End If

			ShowMessage("Parsing " & Path.GetFileName(inputFilePath))

			Dim baseName = Path.GetFileNameWithoutExtension(inputFilePath)
			Dim swMapFile As StreamWriter = Nothing
			Dim mapFilePath As String = Path.Combine(diOutputFolder.FullName, baseName & "_ProteinOrganismMap.txt")

			If Me.CreateProteinToOrganismMapFile Then
				ShowMessage("Creating " & Path.GetFileName(mapFilePath))

				swMapFile = New StreamWriter(New FileStream(mapFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
				swMapFile.WriteLine("Protein" & ControlChars.Tab & "Organism")
			End If

			While oReader.ReadNextProteinEntry()

				Dim organism = ExtractOrganism(oReader.ProteinDescription)
				If String.IsNullOrWhiteSpace(organism) Then
					ShowMessage(" Warning: Organism not found for " & oReader.ProteinName)
					Continue While
				End If

				Dim proteinCount As Integer
				If lstOrganisms.TryGetValue(organism, proteinCount) Then
					lstOrganisms(organism) = proteinCount + 1
				Else
					lstOrganisms.Add(organism, 1)
				End If

				If Not swMapFile Is Nothing Then
					swMapFile.WriteLine(oReader.ProteinName & ControlChars.Tab & organism)
				End If

				intProteinsProcessed += 1
				If intProteinsProcessed Mod 50000 = 0 Then
					ReportProgress("Working: " & oReader.PercentFileProcessed & "% fasta")
				End If

			End While

			If Not swMapFile Is Nothing Then
				swMapFile.Close()
			End If

			Dim organismSummaryFilePath = Path.Combine(diOutputFolder.FullName, baseName & "_OrganismSummary.txt")

			ShowMessage("Creating " & Path.GetFileName(organismSummaryFilePath))

			' Now write out the unique list of organisms
			Using swOrganismSummary = New StreamWriter(New FileStream(organismSummaryFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
				swOrganismSummary.WriteLine(
				  "Organism" & ControlChars.Tab &
				  "Proteins" & ControlChars.Tab &
				  "Genus" & ControlChars.Tab &
				  "Species")

				Dim organismsSorted = (From item In lstOrganisms Select item Order By item.Key)

				Dim lstSquareBrackets = New Char() {"["c, "]"c}

				For Each organism In organismsSorted

					Dim genus As String = String.Empty
					Dim species As String = String.Empty

					Dim nameParts = organism.Key.Split(" "c).ToList()

					If nameParts.Count > 0 Then
						genus = nameParts(0).Trim(lstSquareBrackets)
						If nameParts.Count > 1 Then
							species = nameParts(1)
						End If
					End If

					swOrganismSummary.WriteLine(
					 organism.Key & ControlChars.Tab &
					 organism.Value & ControlChars.Tab &
					 genus & ControlChars.Tab &
					 species)
				Next

			End Using
			
		Catch ex As Exception
			ShowErrorMessage("Error in FindOrganismsInFasta: " & ex.Message)
			Return False
		End Try

		Return True

	End Function

	Private Function ReadOrganismFilterFile(
	  ByVal fiOrganismListFile As FileInfo,
	  <Out()> ByRef lstTextFilters As Dictionary(Of String, Integer),
	  <Out()> ByRef lstRegExFilters As SortedSet(Of Regex)) As Boolean

		lstTextFilters = New Dictionary(Of String, Integer)(StringComparison.CurrentCultureIgnoreCase)
		lstRegExFilters = New SortedSet(Of Regex)

		Dim lineNumber = 0

		Try
			Using srOrganismFilterFile = New StreamReader(New FileStream(fiOrganismListFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))

				While srOrganismFilterFile.Peek > -1
					Dim dataLine = srOrganismFilterFile.ReadLine()
					lineNumber += 1

					If String.IsNullOrWhiteSpace(dataLine) Then Continue While

					' Check for "RegEx:"					
					If dataLine.StartsWith("RegEx:") Then
						Dim regExFilter = dataLine.Substring("RegEx:".Length)
						If String.IsNullOrWhiteSpace(regExFilter) Then
							ShowMessage("  Warning: empty RegEx filter defined on line " & lineNumber)
							Continue While
						End If
						lstRegExFilters.Add(New Regex(regExFilter, RegexOptions.Compiled Or RegexOptions.IgnoreCase))
					ElseIf Not lstTextFilters.ContainsKey(dataLine) Then
						lstTextFilters.Add(dataLine, lineNumber)
					End If

				End While
			End Using
		Catch ex As Exception
			ShowErrorMessage("Error in ReadOrganismFilterFile at line " & lineNumber & ": " & ex.Message)
			Return False
		End Try

		Return True

	End Function

	Private Sub ReportProgress(ByVal strProgress As String)
		Console.WriteLine(System.DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") & " " & strProgress)
	End Sub

	Public Shared Sub ShowErrorMessage(ByVal strMessage As String)
		Const strSeparator As String = "------------------------------------------------------------------------------"

		Console.WriteLine()
		Console.WriteLine(strSeparator)
		Console.WriteLine(strMessage)
		Console.WriteLine(strSeparator)
		Console.WriteLine()

	End Sub

	Private Sub ShowMessage(ByVal message As String)
		Console.WriteLine(message)
	End Sub

	Private Function ValidateInputAndOutputFolders(
	  ByVal inputFilePath As String,
	  ByRef outputFolderPath As String,
	  <Out()> ByRef diOutputFolder As DirectoryInfo) As Boolean

		Dim fiSourceFile = New FileInfo(inputFilePath)
		If Not fiSourceFile.Exists Then
			ShowErrorMessage("Source file not found: " & inputFilePath)
			diOutputFolder = Nothing
			Return False
		End If

		If String.IsNullOrWhiteSpace(outputFolderPath) Then
			outputFolderPath = fiSourceFile.Directory.FullName
		End If

		diOutputFolder = ValidateOutputFolder(outputFolderPath)
		If diOutputFolder Is Nothing Then Return False

		If diOutputFolder.FullName <> fiSourceFile.Directory.FullName Then
			ShowMessage("Output folder: " & diOutputFolder.FullName)
		End If

		Return True

	End Function

	Private Function ValidateOutputFolder(ByRef outputFolderPath As String) As DirectoryInfo
		Try
			If String.IsNullOrWhiteSpace(outputFolderPath) Then
				outputFolderPath = "."
			End If

			Dim diOutputFolder = New DirectoryInfo(outputFolderPath)
			If Not diOutputFolder.Exists Then
				diOutputFolder.Create()
			End If

			Return diOutputFolder

		Catch ex As Exception
			ShowErrorMessage("Error validating the output folder: " & ex.Message)
			Return Nothing
		End Try

	End Function

	Private Sub WriteFastaFileEntry(ByRef swOutFile As StreamWriter, ByVal oReader As ProteinFileReader.FastaFileReader)

		Const RESIDUES_PER_LINE As Integer = 60

		Dim headerLine = oReader.HeaderLine
		Dim spaceIndex = headerLine.IndexOf(" "c)
		If spaceIndex > 0 AndAlso headerLine.Length - spaceIndex >= MAX_PROTEIN_DESCRIPTION_LENGTH Then
			headerLine = headerLine.Substring(0, spaceIndex) + " " + headerLine.Substring(spaceIndex + 1, MAX_PROTEIN_DESCRIPTION_LENGTH)
		End If

		swOutFile.WriteLine(">" & headerLine)

		' Now write out the residues
		Dim intStartIndex = 0
		Dim proteinSequence = oReader.ProteinSequence
		Dim intLength = proteinSequence.Length

		Do While intStartIndex < intLength
			If intStartIndex + RESIDUES_PER_LINE <= intLength Then
				swOutFile.WriteLine(proteinSequence.Substring(intStartIndex, RESIDUES_PER_LINE))
			Else
				swOutFile.WriteLine(proteinSequence.Substring(intStartIndex))
			End If
			intStartIndex += RESIDUES_PER_LINE
		Loop

	End Sub

End Class
