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

    Private Shared Sub AddRegExExpression(lstRegExFilters As IDictionary(Of String, Regex), expression As String)

        If Not lstRegExFilters.ContainsKey(expression) Then
            lstRegExFilters.Add(expression, New Regex(expression, RegexOptions.Compiled Or RegexOptions.IgnoreCase))
        End If
    End Sub

    Private Function ExtractSpecies(proteinDescription As String) As String

        ' Look for the first occurrence of OS=
        ' Adding a bogus extra tag at the end in case the last official tag is OS=
        Dim reMatch = mFindSpeciesTag.Match(proteinDescription & " XX=Ignore")

        If reMatch.Success Then

            Dim speciesTag = reMatch.Groups(1).Value

            Dim reMatch2 = mFindNextTag.Match(speciesTag)

            If reMatch2.Success Then
                Dim species = speciesTag.Substring(0, reMatch2.Index)
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

    Public Function FilterFastaOneOrganism(inputFilePath As String, organismName As String, outputFolderPath As String) As Boolean

        Try

            Dim diOutputFolder As DirectoryInfo = Nothing
            If Not ValidateInputAndOutputFolders(inputFilePath, outputFolderPath, diOutputFolder) Then
                Return False
            End If

            If String.IsNullOrWhiteSpace(organismName) Then
                ConsoleMsgUtils.ShowError("Organism name is empty")
                Return False
            End If

            ' Keys in this dictionary are the organism names to filter on; values are meaningless integers
            ' The reason for using a dictionary is to provide fast lookups, but without case sensitivity
            Dim lstOrganismNameFilters = New Dictionary(Of String, Integer)
            Dim lstRegExFilters = New Dictionary(Of String, Regex)

            If organismName.Contains("*") Then
                AddRegExExpression(lstRegExFilters, organismName.Replace("*", ".+"))
            Else
                lstOrganismNameFilters.Add(organismName, 0)
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

            Dim success = FilterFastaByOrganismWork(inputFilePath, diOutputFolder, lstOrganismNameFilters, lstRegExFilters, outputFileSuffix)

            Return success

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error in FindOrganismsInFasta", ex)
            Return False
        End Try

    End Function

    Public Function FilterFastaByOrganism(inputFilePath As String, organismListFile As String, outputFolderPath As String) As Boolean

        Try

            Dim diOutputFolder As DirectoryInfo = Nothing
            If Not ValidateInputAndOutputFolders(inputFilePath, outputFolderPath, diOutputFolder) Then
                Return False
            End If

            If String.IsNullOrWhiteSpace(organismListFile) Then
                ConsoleMsgUtils.ShowError("Organism list file not defined")
                Return False
            End If

            Dim fiOrganismListFile = New FileInfo(organismListFile)
            If Not fiOrganismListFile.Exists Then
                ConsoleMsgUtils.ShowError("Organism list file not found: " & fiOrganismListFile.FullName)
                Return False
            End If

            ShowMessage("Loading the organism name filters from " & fiOrganismListFile.Name)

            ' Keys in this dictionary are the organism names to filter on; values are meaningless integers
            ' The reason for using a dictionary is to provide fast lookups, but without case sensitivity
            Dim lstTextFilters As Dictionary(Of String, Integer) = Nothing
            Dim lstRegExFilters As Dictionary(Of String, Regex) = Nothing

            If Not ReadNameFilterFile(fiOrganismListFile, lstTextFilters, lstRegExFilters) Then
                Return False
            End If

            If lstTextFilters.Count = 0 AndAlso lstRegExFilters.Count = 0 Then
                ConsoleMsgUtils.ShowError("Organism list file is empty: " & fiOrganismListFile.FullName)
                Return False
            End If

            Dim success = FilterFastaByOrganismWork(inputFilePath, diOutputFolder, lstTextFilters, lstRegExFilters)

            Return success

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error in FilterFastaByOrganism", ex)
            Return False
        End Try

    End Function

    Private Function FilterFastaByOrganismWork(
     inputFilePath As String,
     diOutputFolder As DirectoryInfo,
     lstTextFilters As IDictionary(Of String, Integer),
     lstRegExFilters As Dictionary(Of String, Regex)) As Boolean
        Return FilterFastaByOrganismWork(inputFilePath, diOutputFolder, lstTextFilters, lstRegExFilters, "")
    End Function

    Private Function FilterFastaByOrganismWork(
      inputFilePath As String,
      diOutputFolder As DirectoryInfo,
      lstOrganismNameFilters As IDictionary(Of String, Integer),
      lstRegExFilters As Dictionary(Of String, Regex),
      outputFileSuffix As String) As Boolean

        Dim oReader = New ProteinFileReader.FastaFileReader()
        Dim dtLastProgress = DateTime.UtcNow

        If Not oReader.OpenFile(inputFilePath) Then
            ConsoleMsgUtils.ShowError("Error opening the fasta file; aborting")
            Return False
        End If

        ShowMessage("Parsing " & Path.GetFileName(inputFilePath))

        If String.IsNullOrWhiteSpace(outputFileSuffix) Then
            outputFileSuffix = "_Filtered"
        End If

        Dim baseName = Path.GetFileNameWithoutExtension(inputFilePath)
        Dim filteredFastaFilePath = Path.Combine(diOutputFolder.FullName, baseName & outputFileSuffix & ".fasta")
        Dim swMatchInfoFile As StreamWriter = Nothing

        If VerboseMode Then
            Dim matchInfoFilePath = Path.Combine(diOutputFolder.FullName, baseName & outputFileSuffix & "_MatchInfo.txt")
            swMatchInfoFile = New StreamWriter(New FileStream(matchInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            swMatchInfoFile.WriteLine("Protein" & ControlChars.Tab & "FilterMatch" & ControlChars.Tab & "RegEx")
        End If

        ShowMessage("Creating " & Path.GetFileName(filteredFastaFilePath))

        Using swFilteredFasta = New StreamWriter(New FileStream(filteredFastaFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))

            While oReader.ReadNextProteinEntry()

                Dim species = ExtractSpecies(oReader.ProteinDescription)

                Dim keepProtein = False
                Dim keepProteinFromDescription = False

                If Not String.IsNullOrEmpty(species) Then
                    keepProtein = IsExactOrRegexMatch(oReader.ProteinName, species, lstOrganismNameFilters, lstRegExFilters, VerboseMode, swMatchInfoFile)
                    ' UniProt Fasta file with OS= entries
                Else
                    Dim organism = ExtractOrganism(oReader.ProteinDescription)

                    If Not String.IsNullOrEmpty(organism) Then
                        ' Match organism name within square brackets
                        keepProtein = IsExactOrRegexMatch(oReader.ProteinName, organism, lstOrganismNameFilters, lstRegExFilters, VerboseMode, swMatchInfoFile)
                    End If

                    If Not keepProtein Then
                        ' Match the entire protein description
                        keepProteinFromDescription = IsExactOrRegexMatch(oReader.ProteinName, oReader.ProteinDescription, lstOrganismNameFilters, lstRegExFilters, VerboseMode, swMatchInfoFile)
                    End If
                End If

                If keepProtein Or keepProteinFromDescription Then
                    WriteFastaFileEntry(swFilteredFasta, oReader)
                End If

                If DateTime.UtcNow.Subtract(dtLastProgress).TotalSeconds >= 10 Then
                    dtLastProgress = DateTime.UtcNow
                    If VerboseMode Then Console.WriteLine()
                    If VerboseMode Then Console.WriteLine("--------------------------------------------")
                    ReportProgress("Working: " & oReader.PercentFileProcessed & "% complete")
                    If VerboseMode Then Console.WriteLine("--------------------------------------------")
                    If VerboseMode Then Console.WriteLine()
                End If

            End While

        End Using

        If Not swMatchInfoFile Is Nothing Then
            swMatchInfoFile.Close()
        End If

        Return True

    End Function

    Private Shared Function IsExactOrRegexMatch(
       proteinName As String,
       textToSearch As String,
       itemsToMatchExactly As IDictionary(Of String, Integer),
       regExFilters As Dictionary(Of String, Regex),
       showMessages As Boolean,
       swMatchInfoFile As TextWriter) As Boolean

        Dim keepProtein = False

        If itemsToMatchExactly.ContainsKey(textToSearch) Then
            keepProtein = True
            If showMessages Then
                Console.WriteLine("Protein " & proteinName & " matched " & textToSearch)
            End If
            If Not swMatchInfoFile Is Nothing Then
                swMatchInfoFile.WriteLine(proteinName & ControlChars.Tab & textToSearch)
            End If
        Else
            For Each regExSpec In regExFilters
                Dim reMatch = regExSpec.Value.Match(textToSearch)
                If reMatch.Success Then
                    keepProtein = True

                    If showMessages Then
                        Dim contextIndexStart = reMatch.Index - 5
                        Dim contextIndexEnd = reMatch.Index + reMatch.Value.Length + 10
                        If contextIndexStart < 0 Then contextIndexStart = 0
                        If contextIndexEnd >= textToSearch.Length Then contextIndexEnd = textToSearch.Length - 1

                        Console.WriteLine("Protein " & proteinName & " matched " & reMatch.Value & " in: " &
                                          textToSearch.Substring(contextIndexStart, contextIndexEnd - contextIndexStart))
                    End If

                    If Not swMatchInfoFile Is Nothing Then
                        swMatchInfoFile.WriteLine(proteinName & ControlChars.Tab & reMatch.Value & ControlChars.Tab & regExSpec.Key)
                    End If

                    Exit For
                End If
            Next
        End If

        Return keepProtein

    End Function

    Public Function FilterFastaByProteinName(inputFilePath As String, proteinListFile As String, outputFolderPath As String) As Boolean

        Try

            Dim diOutputFolder As DirectoryInfo = Nothing
            If Not ValidateInputAndOutputFolders(inputFilePath, outputFolderPath, diOutputFolder) Then
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
            Dim lstTextFilters As Dictionary(Of String, Integer) = Nothing
            Dim lstRegExFilters As Dictionary(Of String, Regex) = Nothing

            If Not ReadNameFilterFile(fiProteinListFile, lstTextFilters, lstRegExFilters) Then
                Return False
            End If

            If lstTextFilters.Count = 0 AndAlso lstRegExFilters.Count = 0 Then
                ConsoleMsgUtils.ShowError("Protein list file is empty: " & fiProteinListFile.FullName)
                Return False
            End If

            Dim success = FilterFastaByProteinWork(inputFilePath, diOutputFolder, lstTextFilters, lstRegExFilters)

            Return success

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error in FilterProteinName", ex)
            Return False
        End Try

    End Function

    Private Function FilterFastaByProteinWork(
     inputFilePath As String,
     diOutputFolder As DirectoryInfo,
     lstTextFilters As IDictionary(Of String, Integer),
     lstRegExFilters As Dictionary(Of String, Regex)) As Boolean
        Return FilterFastaByProteinWork(inputFilePath, diOutputFolder, lstTextFilters, lstRegExFilters, "")
    End Function

    Private Function FilterFastaByProteinWork(
      inputFilePath As String,
      diOutputFolder As DirectoryInfo,
      lstTextFilters As IDictionary(Of String, Integer),
      lstRegExFilters As Dictionary(Of String, Regex),
      outputFileSuffix As String) As Boolean

        Dim oReader = New ProteinFileReader.FastaFileReader()
        Dim dtLastProgress = DateTime.UtcNow

        If Not oReader.OpenFile(inputFilePath) Then
            ConsoleMsgUtils.ShowError("Error opening the fasta file; aborting")
            Return False
        End If

        ShowMessage("Parsing " & Path.GetFileName(inputFilePath))

        If String.IsNullOrWhiteSpace(outputFileSuffix) Then
            outputFileSuffix = "_Filtered"
        End If

        Dim baseName = Path.GetFileNameWithoutExtension(inputFilePath)
        Dim filteredFastaFilePath = Path.Combine(diOutputFolder.FullName, baseName & outputFileSuffix & ".fasta")
        Dim swMatchInfoFile As StreamWriter = Nothing

        If VerboseMode Then
            Dim matchInfoFilePath = Path.Combine(diOutputFolder.FullName, baseName & outputFileSuffix & "_MatchInfo.txt")
            swMatchInfoFile = New StreamWriter(New FileStream(matchInfoFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            swMatchInfoFile.WriteLine("Protein" & ControlChars.Tab & "FilterMatch" & ControlChars.Tab & "RegEx")
        End If

        ShowMessage("Creating " & Path.GetFileName(filteredFastaFilePath))

        Using swFilteredFasta = New StreamWriter(New FileStream(filteredFastaFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))

            While oReader.ReadNextProteinEntry()

                Dim keepProtein = IsExactOrRegexMatch(oReader.ProteinName, oReader.ProteinName, lstTextFilters, lstRegExFilters, VerboseMode, swMatchInfoFile)
                Dim keepProteinFromDescription = False

                If Not keepProtein AndAlso SearchProteinDescriptions Then
                    keepProteinFromDescription = IsExactOrRegexMatch(oReader.ProteinName, oReader.ProteinDescription, lstTextFilters, lstRegExFilters, VerboseMode, swMatchInfoFile)
                End If

                If keepProtein Or keepProteinFromDescription Then
                    WriteFastaFileEntry(swFilteredFasta, oReader)
                End If

                If DateTime.UtcNow.Subtract(dtLastProgress).TotalSeconds >= 10 Then
                    dtLastProgress = DateTime.UtcNow
                    If VerboseMode Then Console.WriteLine()
                    If VerboseMode Then Console.WriteLine("--------------------------------------------")
                    ReportProgress("Working: " & oReader.PercentFileProcessed & "% complete")
                    If VerboseMode Then Console.WriteLine("--------------------------------------------")
                    If VerboseMode Then Console.WriteLine()
                End If

            End While

        End Using

        If Not swMatchInfoFile Is Nothing Then
            swMatchInfoFile.Close()
        End If

        Return True

    End Function

    Public Function FindOrganismsInFasta(inputFilePath As String, outputFolderPath As String) As Boolean

        Try

            Dim oReader = New ProteinFileReader.FastaFileReader()
            Dim dtLastProgress = DateTime.UtcNow

            Dim diOutputFolder As DirectoryInfo = Nothing
            If Not ValidateInputAndOutputFolders(inputFilePath, outputFolderPath, diOutputFolder) Then
                Return False
            End If

            ' Key is organism name, value is protein usage count
            Dim lstOrganisms = New Dictionary(Of String, Integer)

            If Not oReader.OpenFile(inputFilePath) Then
                ConsoleMsgUtils.ShowError("Error opening the fasta file; aborting")
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

                Dim organism = ExtractSpecies(oReader.ProteinDescription)

                If String.IsNullOrEmpty(organism) Then
                    organism = ExtractOrganism(oReader.ProteinDescription)
                End If

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

                If DateTime.UtcNow.Subtract(dtLastProgress).TotalSeconds >= 10 Then
                    dtLastProgress = DateTime.UtcNow
                    If VerboseMode Then Console.WriteLine()
                    If VerboseMode Then Console.WriteLine("--------------------------------------------")
                    ReportProgress("Working: " & oReader.PercentFileProcessed & "% complete")
                    If VerboseMode Then Console.WriteLine("--------------------------------------------")
                    If VerboseMode Then Console.WriteLine()
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
            ConsoleMsgUtils.ShowError("Error in FindOrganismsInFasta", ex)
            Return False
        End Try

        Return True

    End Function

    Private Function ReadNameFilterFile(
      fiNameListFile As FileInfo,
      <Out()> ByRef lstTextFilters As Dictionary(Of String, Integer),
      <Out()> ByRef lstRegExFilters As Dictionary(Of String, Regex)) As Boolean

        lstTextFilters = New Dictionary(Of String, Integer)(StringComparison.CurrentCultureIgnoreCase)
        lstRegExFilters = New Dictionary(Of String, Regex)

        Dim lineNumber = 0

        Try
            Using srFilterFile = New StreamReader(New FileStream(fiNameListFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))

                While Not srFilterFile.EndOfStream
                    Dim dataLine = srFilterFile.ReadLine()
                    lineNumber += 1

                    If String.IsNullOrWhiteSpace(dataLine) Then Continue While

                    ' Check for "RegEx:"
                    If dataLine.StartsWith("RegEx:") Then
                        Dim regExFilter = dataLine.Substring("RegEx:".Length)
                        If String.IsNullOrWhiteSpace(regExFilter) Then
                            ShowMessage("  Warning: empty RegEx filter defined on line " & lineNumber)
                            Continue While
                        End If
                        AddRegExExpression(lstRegExFilters, regExFilter)
                    ElseIf Not lstTextFilters.ContainsKey(dataLine) Then
                        lstTextFilters.Add(dataLine, lineNumber)
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
        Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") & " " & strProgress)
    End Sub

    Private Sub ShowMessage(message As String)
        Console.WriteLine(message)
    End Sub

    Private Function ValidateInputAndOutputFolders(
      inputFilePath As String,
      ByRef outputFolderPath As String,
      <Out()> ByRef diOutputFolder As DirectoryInfo) As Boolean

        Dim fiSourceFile = New FileInfo(inputFilePath)
        If Not fiSourceFile.Exists Then
            ConsoleMsgUtils.ShowError("Source file not found: " & inputFilePath)
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
            ConsoleMsgUtils.ShowError("Error validating the output folder", ex)
            Return Nothing
        End Try

    End Function

    Private Sub WriteFastaFileEntry(ByRef swOutFile As StreamWriter, oReader As ProteinFileReader.FastaFileReader)

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
