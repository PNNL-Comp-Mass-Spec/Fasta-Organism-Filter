Option Strict On

Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text.RegularExpressions
Imports PRISM


''' <summary>
''' This class reads a fasta file and finds the organism info defined in the protein description lines
''' It optionally creates a filtered fasta file containing only the proteins of interest
''' </summary>
''' <remarks>
''' <para>Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
''' Program started September 4, 2014
''' Copyright 2014, Battelle Memorial Institute.  All Rights Reserved.
''' </para>
''' <para>
''' E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
''' Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/
''' </para>
''' <para>
''' Licensed under the Apache License, Version 2.0; you may not use this file except
''' in compliance with the License.  You may obtain a copy of the License at
''' http://www.apache.org/licenses/LICENSE-2.0
''' </para>
''' </remarks>
Public Class FilterFastaByOrganism


    Protected Const MAX_PROTEIN_DESCRIPTION_LENGTH As Integer = 7500

    Private ReadOnly mFindSpeciesTag As Regex
    Private ReadOnly mFindNextTag As Regex

    ''' <summary>
    ''' Create a protein to organism map file
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Property CreateProteinToOrganismMapFile As Boolean

    ''' <summary>
    ''' Also search protein descriptions in addition to protein names
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Property SearchProteinDescriptions As Boolean

    ''' <summary>
    ''' Show additional messages when true, including which search term or RegEx resulted in a match
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Property VerboseMode As Boolean

    ''' <summary>
    ''' Constructor
    ''' </summary>
    Public Sub New()
        mFindSpeciesTag = New Regex("OS=(.+)", RegexOptions.Compiled Or RegexOptions.IgnoreCase)
        mFindNextTag = New Regex(" [a-z]+=", RegexOptions.Compiled Or RegexOptions.IgnoreCase)
    End Sub

    Private Shared Sub AddRegExExpression(regExFilters As IDictionary(Of String, Regex), expression As String)

        If Not regExFilters.ContainsKey(expression) Then
            regExFilters.Add(expression, New Regex(expression, RegexOptions.Compiled Or RegexOptions.IgnoreCase))
        End If
    End Sub

    Private Function ExtractSpecies(proteinDescription As String) As String

        ' Look for the first occurrence of OS=
        ' Adding a bogus extra tag at the end in case the last official tag is OS=
        Dim match = mFindSpeciesTag.Match(proteinDescription & " XX=Ignore")

        If match.Success Then

            Dim speciesTag = match.Groups(1).Value

            Dim match2 = mFindNextTag.Match(speciesTag)

            If match2.Success Then
                Dim species = speciesTag.Substring(0, match2.Index)
                Return species
            End If

        End If

        Return String.Empty

    End Function

    ''' <summary>
    ''' Search for the organism info, assuming it is the last text seen between two square brackets
    ''' </summary>
    ''' <param name="proteinDescription"></param>
    ''' <returns></returns>
    ''' <remarks>
    ''' However, there are exceptions we have to consider, for example
    ''' [Salmonella enterica subsp. enterica serovar 4,[5],12:i:-]
    ''' </remarks>
    Private Function ExtractOrganism(proteinDescription As String) As String

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

    Public Function FilterFastaOneOrganism(inputFilePath As String, organismName As String, outputDirectoryPath As String) As Boolean

        Try

            Dim outputDirectory As DirectoryInfo = Nothing
            If Not ValidateInputAndOutputDirectories(inputFilePath, outputDirectoryPath, outputDirectory) Then
                Return False
            End If

            If String.IsNullOrWhiteSpace(organismName) Then
                ConsoleMsgUtils.ShowError("Organism name is empty")
                Return False
            End If

            ' Keys in this dictionary are the organism names to filter on; values are meaningless integers
            ' The reason for using a dictionary is to provide fast lookups, but without case sensitivity
            Dim organismNameFilters = New Dictionary(Of String, Integer)
            Dim regExFilters = New Dictionary(Of String, Regex)

            If organismName.Contains("*") Then
                AddRegExExpression(regExFilters, organismName.Replace("*", ".+"))
            Else
                organismNameFilters.Add(organismName, 0)
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

            Dim success = FilterFastaByOrganismWork(inputFilePath, outputDirectory, organismNameFilters, regExFilters, outputFileSuffix)

            Return success

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error in FindOrganismsInFasta", ex)
            Return False
        End Try

    End Function

    Public Function FilterFastaByOrganism(inputFilePath As String, organismListFilePath As String, outputDirectoryPath As String) As Boolean

        Try

            Dim outputDirectory As DirectoryInfo = Nothing
            If Not ValidateInputAndOutputDirectories(inputFilePath, outputDirectoryPath, outputDirectory) Then
                Return False
            End If

            If String.IsNullOrWhiteSpace(organismListFilePath) Then
                ConsoleMsgUtils.ShowError("Organism list file not defined")
                Return False
            End If

            Dim organismListFile = New FileInfo(organismListFilePath)
            If Not organismListFile.Exists Then
                ConsoleMsgUtils.ShowError("Organism list file not found: " & organismListFile.FullName)
                Return False
            End If

            ShowMessage("Loading the organism name filters from " & organismListFile.Name)

            ' Keys in this dictionary are the organism names to filter on; values are meaningless integers
            ' The reason for using a dictionary is to provide fast lookups, but without case sensitivity
            Dim textFilters As Dictionary(Of String, Integer) = Nothing
            Dim regExFilters As Dictionary(Of String, Regex) = Nothing

            If Not ReadNameFilterFile(organismListFile, textFilters, regExFilters) Then
                Return False
            End If

            If textFilters.Count = 0 AndAlso regExFilters.Count = 0 Then
                ConsoleMsgUtils.ShowError("Organism list file is empty: " & organismListFile.FullName)
                Return False
            End If

            Dim success = FilterFastaByOrganismWork(inputFilePath, outputDirectory, textFilters, regExFilters)

            Return success

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error in FilterFastaByOrganism", ex)
            Return False
        End Try

    End Function

    Private Function FilterFastaByOrganismWork(
     inputFilePath As String,
     outputDirectory As DirectoryInfo,
     textFilters As IDictionary(Of String, Integer),
     regExFilters As Dictionary(Of String, Regex)) As Boolean
        Return FilterFastaByOrganismWork(inputFilePath, outputDirectory, textFilters, regExFilters, "")
    End Function

    ' ReSharper disable once SuggestBaseTypeForParameter
    Private Function FilterFastaByOrganismWork(
      inputFilePath As String,
      outputDirectory As DirectoryInfo,
      organismNameFilters As IDictionary(Of String, Integer),
      regExFilters As Dictionary(Of String, Regex),
      outputFileSuffix As String) As Boolean

        Dim reader = New FastaFileReader()
        Dim lastProgressTime = DateTime.UtcNow

        If Not reader.OpenFile(inputFilePath) Then
            ConsoleMsgUtils.ShowError("Error opening the fasta file; aborting")
            Return False
        End If

        ShowMessage("Parsing " & Path.GetFileName(inputFilePath))

        If String.IsNullOrWhiteSpace(outputFileSuffix) Then
            outputFileSuffix = "_Filtered"
        End If

        Dim baseName = Path.GetFileNameWithoutExtension(inputFilePath)
        Dim filteredFastaFilePath = Path.Combine(outputDirectory.FullName, baseName & outputFileSuffix & ".fasta")
        Dim matchInfoWriter As StreamWriter = Nothing

        If VerboseMode Then
            Dim matchInfoFilePath = Path.Combine(outputDirectory.FullName, baseName & outputFileSuffix & "_MatchInfo.txt")
            matchInfoWriter = New StreamWriter(New FileStream(matchInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            matchInfoWriter.WriteLine("Protein" & ControlChars.Tab & "FilterMatch" & ControlChars.Tab & "RegEx")
        End If

        ShowMessage("Creating " & Path.GetFileName(filteredFastaFilePath))

        Using writer = New StreamWriter(New FileStream(filteredFastaFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))

            While reader.ReadNextProteinEntry()

                Dim species = ExtractSpecies(reader.ProteinDescription)

                Dim keepProtein = False
                Dim keepProteinFromDescription = False

                If Not String.IsNullOrEmpty(species) Then
                    ' UniProt Fasta file with OS= entries
                    keepProtein = IsExactOrRegexMatch(reader.ProteinName, species, organismNameFilters, regExFilters, VerboseMode, matchInfoWriter)
                Else
                    Dim organism = ExtractOrganism(reader.ProteinDescription)

                    If Not String.IsNullOrEmpty(organism) Then
                        ' Match organism name within square brackets
                        keepProtein = IsExactOrRegexMatch(reader.ProteinName, organism, organismNameFilters, regExFilters, VerboseMode, matchInfoWriter)
                    End If

                    If Not keepProtein Then
                        ' Match the entire protein description
                        keepProteinFromDescription = IsExactOrRegexMatch(reader.ProteinName, reader.ProteinDescription, organismNameFilters, regExFilters, VerboseMode, matchInfoWriter)
                    End If
                End If

                If keepProtein Or keepProteinFromDescription Then
                    WriteFastaFileEntry(writer, reader)
                End If

                If DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds < 10 Then Continue While

                lastProgressTime = DateTime.UtcNow
                If VerboseMode Then Console.WriteLine()
                If VerboseMode Then Console.WriteLine("--------------------------------------------")
                ReportProgress("Working: " & reader.PercentFileProcessed & "% complete")
                If VerboseMode Then Console.WriteLine("--------------------------------------------")
                If VerboseMode Then Console.WriteLine()

            End While

        End Using

        If matchInfoWriter IsNot Nothing Then
            matchInfoWriter.Close()
        End If

        Return True

    End Function

    Private Shared Function IsExactOrRegexMatch(
       proteinName As String,
       textToSearch As String,
       itemsToMatchExactly As IDictionary(Of String, Integer),
       regExFilters As Dictionary(Of String, Regex),
       showMessages As Boolean,
       matchInfoWriter As TextWriter) As Boolean

        Dim keepProtein = False

        If itemsToMatchExactly.ContainsKey(textToSearch) Then
            keepProtein = True
            If showMessages Then
                Console.WriteLine("Protein " & proteinName & " matched " & textToSearch)
            End If
            If matchInfoWriter IsNot Nothing Then
                matchInfoWriter.WriteLine(proteinName & ControlChars.Tab & textToSearch)
            End If
        Else
            For Each regExSpec In regExFilters
                Dim match = regExSpec.Value.Match(textToSearch)
                If match.Success Then
                    keepProtein = True

                    If showMessages Then
                        Dim contextIndexStart = match.Index - 5
                        Dim contextIndexEnd = match.Index + match.Value.Length + 10
                        If contextIndexStart < 0 Then contextIndexStart = 0
                        If contextIndexEnd >= textToSearch.Length Then contextIndexEnd = textToSearch.Length - 1

                        Console.WriteLine("Protein " & proteinName & " matched " & match.Value & " in: " &
                                          textToSearch.Substring(contextIndexStart, contextIndexEnd - contextIndexStart))
                    End If

                    If matchInfoWriter IsNot Nothing Then
                        matchInfoWriter.WriteLine(proteinName & ControlChars.Tab & match.Value & ControlChars.Tab & regExSpec.Key)
                    End If

                    Exit For
                End If
            Next
        End If

        Return keepProtein

    End Function

    Public Function FilterFastaByProteinName(inputFilePath As String, proteinListFile As String, outputDirectoryPath As String) As Boolean

        Try

            Dim outputDirectory As DirectoryInfo = Nothing
            If Not ValidateInputAndOutputDirectories(inputFilePath, outputDirectoryPath, outputDirectory) Then
                Return False
            End If

            If String.IsNullOrWhiteSpace(proteinListFile) Then
                ConsoleMsgUtils.ShowError("Protein list file not defined")
                Return False
            End If

            Dim fiProteinListFile = New FileInfo(proteinListFile)
            If Not fiProteinListFile.Exists Then
                ConsoleMsgUtils.ShowError("Protein list file not found: " & fiProteinListFile.FullName)
                Return False
            End If

            ShowMessage("Loading the protein name filters from " & fiProteinListFile.Name)

            ' Keys in this dictionary are the protein names to filter on; values are meaningless integers
            ' The reason for using a dictionary is to provide fast lookups, but without case sensitivity
            Dim textFilters As Dictionary(Of String, Integer) = Nothing
            Dim regExFilters As Dictionary(Of String, Regex) = Nothing

            If Not ReadNameFilterFile(fiProteinListFile, textFilters, regExFilters) Then
                Return False
            End If

            If textFilters.Count = 0 AndAlso regExFilters.Count = 0 Then
                ConsoleMsgUtils.ShowError("Protein list file is empty: " & fiProteinListFile.FullName)
                Return False
            End If

            Dim success = FilterFastaByProteinWork(inputFilePath, outputDirectory, textFilters, regExFilters)

            Return success

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error in FilterProteinName", ex)
            Return False
        End Try

    End Function

    Private Function FilterFastaByProteinWork(
     inputFilePath As String,
     outputDirectory As DirectoryInfo,
     textFilters As IDictionary(Of String, Integer),
     regExFilters As Dictionary(Of String, Regex)) As Boolean
        Return FilterFastaByProteinWork(inputFilePath, outputDirectory, textFilters, regExFilters, "")
    End Function

    ' ReSharper disable once SuggestBaseTypeForParameter
    Private Function FilterFastaByProteinWork(
      inputFilePath As String,
      outputDirectory As DirectoryInfo,
      textFilters As IDictionary(Of String, Integer),
      regExFilters As Dictionary(Of String, Regex),
      outputFileSuffix As String) As Boolean

        Dim reader = New FastaFileReader()
        Dim lastProgressTime = DateTime.UtcNow

        If Not reader.OpenFile(inputFilePath) Then
            ConsoleMsgUtils.ShowError("Error opening the fasta file; aborting")
            Return False
        End If

        ShowMessage("Parsing " & Path.GetFileName(inputFilePath))

        If String.IsNullOrWhiteSpace(outputFileSuffix) Then
            outputFileSuffix = "_Filtered"
        End If

        Dim baseName = Path.GetFileNameWithoutExtension(inputFilePath)
        Dim filteredFastaFilePath = Path.Combine(outputDirectory.FullName, baseName & outputFileSuffix & ".fasta")
        Dim matchInfoWriter As StreamWriter = Nothing

        If VerboseMode Then
            Dim matchInfoFilePath = Path.Combine(outputDirectory.FullName, baseName & outputFileSuffix & "_MatchInfo.txt")
            matchInfoWriter = New StreamWriter(New FileStream(matchInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            matchInfoWriter.WriteLine("Protein" & ControlChars.Tab & "FilterMatch" & ControlChars.Tab & "RegEx")
        End If

        ShowMessage("Creating " & Path.GetFileName(filteredFastaFilePath))

        Using writer = New StreamWriter(New FileStream(filteredFastaFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))

            While reader.ReadNextProteinEntry()

                Dim keepProtein = IsExactOrRegexMatch(reader.ProteinName, reader.ProteinName, textFilters, regExFilters, VerboseMode, matchInfoWriter)
                Dim keepProteinFromDescription = False

                If Not keepProtein AndAlso SearchProteinDescriptions Then
                    keepProteinFromDescription = IsExactOrRegexMatch(reader.ProteinName, reader.ProteinDescription, textFilters, regExFilters, VerboseMode, matchInfoWriter)
                End If

                If keepProtein Or keepProteinFromDescription Then
                    WriteFastaFileEntry(writer, reader)
                End If

                If DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds < 10 Then Continue While

                lastProgressTime = DateTime.UtcNow
                If VerboseMode Then Console.WriteLine()
                If VerboseMode Then Console.WriteLine("--------------------------------------------")
                ReportProgress("Working: " & reader.PercentFileProcessed & "% complete")
                If VerboseMode Then Console.WriteLine("--------------------------------------------")
                If VerboseMode Then Console.WriteLine()


            End While

        End Using

        If matchInfoWriter IsNot Nothing Then
            matchInfoWriter.Close()
        End If

        Return True

    End Function

    Public Function FindOrganismsInFasta(inputFilePath As String, outputDirectoryPath As String) As Boolean

        Try

            Dim reader = New FastaFileReader()
            Dim lastProgressTime = DateTime.UtcNow

            Dim outputDirectory As DirectoryInfo = Nothing
            If Not ValidateInputAndOutputDirectories(inputFilePath, outputDirectoryPath, outputDirectory) Then
                Return False
            End If

            ' Key is organism name, value is protein usage count
            Dim lstOrganisms = New Dictionary(Of String, Integer)

            If Not reader.OpenFile(inputFilePath) Then
                ConsoleMsgUtils.ShowError("Error opening the fasta file; aborting")
                Return False
            End If

            ShowMessage("Parsing " & Path.GetFileName(inputFilePath))

            Dim baseName = Path.GetFileNameWithoutExtension(inputFilePath)
            Dim mapFileWriter As StreamWriter = Nothing
            Dim mapFilePath As String = Path.Combine(outputDirectory.FullName, baseName & "_ProteinOrganismMap.txt")

            If Me.CreateProteinToOrganismMapFile Then
                ShowMessage("Creating " & Path.GetFileName(mapFilePath))

                mapFileWriter = New StreamWriter(New FileStream(mapFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                mapFileWriter.WriteLine("Protein" & ControlChars.Tab & "Organism")
            End If

            While reader.ReadNextProteinEntry()

                Dim organism = ExtractSpecies(reader.ProteinDescription)

                If String.IsNullOrEmpty(organism) Then
                    organism = ExtractOrganism(reader.ProteinDescription)
                End If

                If String.IsNullOrWhiteSpace(organism) Then
                    ShowMessage(" Warning: Organism not found for " & reader.ProteinName)
                    Continue While
                End If

                Dim proteinCount As Integer
                If lstOrganisms.TryGetValue(organism, proteinCount) Then
                    lstOrganisms(organism) = proteinCount + 1
                Else
                    lstOrganisms.Add(organism, 1)
                End If

                If mapFileWriter IsNot Nothing Then
                    mapFileWriter.WriteLine(reader.ProteinName & ControlChars.Tab & organism)
                End If

                If DateTime.UtcNow.Subtract(lastProgressTime).TotalSeconds >= 10 Then
                    lastProgressTime = DateTime.UtcNow
                    If VerboseMode Then Console.WriteLine()
                    If VerboseMode Then Console.WriteLine("--------------------------------------------")
                    ReportProgress("Working: " & reader.PercentFileProcessed & "% complete")
                    If VerboseMode Then Console.WriteLine("--------------------------------------------")
                    If VerboseMode Then Console.WriteLine()
                End If

            End While

            If mapFileWriter IsNot Nothing Then
                mapFileWriter.Close()
            End If

            Dim organismSummaryFilePath = Path.Combine(outputDirectory.FullName, baseName & "_OrganismSummary.txt")

            ShowMessage("Creating " & Path.GetFileName(organismSummaryFilePath))

            ' Now write out the unique list of organisms
            Using summaryWriter = New StreamWriter(New FileStream(organismSummaryFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))

                summaryWriter.WriteLine(
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

                    summaryWriter.WriteLine(
                     organism.Key & ControlChars.Tab &
                     organism.Value & ControlChars.Tab &
                     genus & ControlChars.Tab &
                     species)
                Next

            End Using

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error in FindOrganismsInFasta", ex)
            Return False
        End Try

        Return True

    End Function

    ' ReSharper disable once SuggestBaseTypeForParameter
    Private Function ReadNameFilterFile(
      nameListFile As FileInfo,
      <Out()> ByRef textFilters As Dictionary(Of String, Integer),
      <Out()> ByRef regExFilters As Dictionary(Of String, Regex)) As Boolean

        textFilters = New Dictionary(Of String, Integer)(StringComparison.CurrentCultureIgnoreCase)
        regExFilters = New Dictionary(Of String, Regex)

        Dim lineNumber = 0

        Try
            Using reader = New StreamReader(New FileStream(nameListFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))

                While Not reader.EndOfStream
                    Dim dataLine = reader.ReadLine()
                    lineNumber += 1

                    If String.IsNullOrWhiteSpace(dataLine) Then Continue While

                    ' Check for "RegEx:"
                    If dataLine.StartsWith("RegEx:") Then
                        Dim regExFilter = dataLine.Substring("RegEx:".Length)
                        If String.IsNullOrWhiteSpace(regExFilter) Then
                            ShowMessage("  Warning: empty RegEx filter defined on line " & lineNumber)
                            Continue While
                        End If
                        AddRegExExpression(regExFilters, regExFilter)
                    ElseIf Not textFilters.ContainsKey(dataLine) Then
                        textFilters.Add(dataLine, lineNumber)
                    End If

                End While
            End Using
        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error in ReadNameFilterFile at line " & lineNumber, ex.Message)
            Return False
        End Try

        Return True

    End Function

    Private Sub ReportProgress(strProgress As String)
        Console.WriteLine(Date.Now.ToString("yyyy-MM-dd hh:mm:ss") & " " & strProgress)
    End Sub

    Private Sub ShowMessage(message As String)
        Console.WriteLine(message)
    End Sub

    Private Function ValidateInputAndOutputDirectories(
      inputFilePath As String,
      ByRef outputDirectoryPath As String,
      <Out()> ByRef outputDirectory As DirectoryInfo) As Boolean

        Dim fiSourceFile = New FileInfo(inputFilePath)
        If Not fiSourceFile.Exists Then
            ConsoleMsgUtils.ShowError("Source file not found: " & inputFilePath)
            outputDirectory = Nothing
            Return False
        End If

        If String.IsNullOrWhiteSpace(outputDirectoryPath) Then
            outputDirectoryPath = fiSourceFile.Directory.FullName
        End If

        outputDirectory = ValidateOutputDirectory(outputDirectoryPath)
        If outputDirectory Is Nothing Then Return False

        If outputDirectory.FullName <> fiSourceFile.Directory.FullName Then
            ShowMessage("Output directory: " & outputDirectory.FullName)
        End If

        Return True

    End Function

    Private Function ValidateOutputDirectory(ByRef outputDirectoryPath As String) As DirectoryInfo
        Try
            If String.IsNullOrWhiteSpace(outputDirectoryPath) Then
                outputDirectoryPath = "."
            End If

            Dim outputDirectory = New DirectoryInfo(outputDirectoryPath)
            If Not outputDirectory.Exists Then
                outputDirectory.Create()
            End If

            Return outputDirectory

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error validating the output directory", ex)
            Return Nothing
        End Try

    End Function

    Private Sub WriteFastaFileEntry(ByRef writer As StreamWriter, reader As ProteinFileReaderBaseClass)

        Const RESIDUES_PER_LINE = 60

        Dim headerLine = reader.HeaderLine
        Dim spaceIndex = headerLine.IndexOf(" "c)
        If spaceIndex > 0 AndAlso headerLine.Length - spaceIndex >= MAX_PROTEIN_DESCRIPTION_LENGTH Then
            headerLine = headerLine.Substring(0, spaceIndex) + " " + headerLine.Substring(spaceIndex + 1, MAX_PROTEIN_DESCRIPTION_LENGTH)
        End If

        writer.WriteLine(">" & headerLine)

        ' Now write out the residues
        Dim intStartIndex = 0
        Dim proteinSequence = reader.ProteinSequence
        Dim intLength = proteinSequence.Length

        Do While intStartIndex < intLength
            If intStartIndex + RESIDUES_PER_LINE <= intLength Then
                writer.WriteLine(proteinSequence.Substring(intStartIndex, RESIDUES_PER_LINE))
            Else
                writer.WriteLine(proteinSequence.Substring(intStartIndex))
            End If
            intStartIndex += RESIDUES_PER_LINE
        Loop

    End Sub

End Class
