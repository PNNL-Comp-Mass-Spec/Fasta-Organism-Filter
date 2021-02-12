' This program reads a fasta file and finds the organism info defined in the protein description lines
' It optionally creates a filtered fasta file containing only the proteins of interest
'
' -------------------------------------------------------------------------------
' Written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA)
' Program started September 4, 2014
' Copyright 2014, Battelle Memorial Institute.  All Rights Reserved.

' E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com
' Website: http://omics.pnl.gov/ or http://www.sysbio.org/resources/staff/ or http://panomics.pnnl.gov/
' -------------------------------------------------------------------------------
'
' Licensed under the Apache License, Version 2.0; you may not use this file except
' in compliance with the License.  You may obtain a copy of the License at
' http://www.apache.org/licenses/LICENSE-2.0
'
Imports PRISM

Module modMain

    Public Const PROGRAM_DATE As String = "February 11, 2021"

    Private mInputFilePath As String
    Private mOutputFolderPath As String
    Private mOrganismListFile As String
    Private mOrganismName As String
    Private mProteinListFile As String

    Private mCreateProteinToOrganismMapFile As Boolean
    Private mSearchProteinDescriptions As Boolean
    Private mVerboseMode As Boolean

    ' Ignore Spelling: Prot, Desc, UniProt

    Public Function Main() As Integer

        Try
            ' Set the default values
            mInputFilePath = String.Empty

            mOrganismListFile = String.Empty
            mOrganismName = String.Empty
            mProteinListFile = String.Empty

            Dim proceed = False
            Dim commandLineParser As New clsParseCommandLine
            If commandLineParser.ParseCommandLine Then
                If SetOptionsUsingCommandLineParameters(commandLineParser) Then proceed = True
            End If

            If Not proceed OrElse
               commandLineParser.NeedToShowHelp OrElse
               commandLineParser.ParameterCount + commandLineParser.NonSwitchParameterCount = 0 OrElse
               mInputFilePath.Length = 0 Then
                ShowProgramHelp()
                Return -1
            End If

            Dim organismFilter = New FilterFastaByOrganism()

            With organismFilter

                .CreateProteinToOrganismMapFile = mCreateProteinToOrganismMapFile
                .SearchProteinDescriptions = mSearchProteinDescriptions
                .VerboseMode = mVerboseMode

                ''If Not mParameterFilePath Is Nothing AndAlso mParameterFilePath.Length > 0 Then
                ''    .LoadParameterFileSettings(mParameterFilePath)
                ''End If
            End With

            Dim success As Boolean

            If Not String.IsNullOrEmpty(mOrganismName) Then
                success = organismFilter.FilterFastaOneOrganism(mInputFilePath, mOrganismName, mOutputFolderPath)

            ElseIf Not String.IsNullOrEmpty(mOrganismListFile) Then
                success = organismFilter.FilterFastaByOrganism(mInputFilePath, mOrganismListFile, mOutputFolderPath)

            ElseIf Not String.IsNullOrEmpty(mProteinListFile) Then
                success = organismFilter.FilterFastaByProteinName(mInputFilePath, mProteinListFile, mOutputFolderPath)

            Else
                success = organismFilter.FindOrganismsInFasta(mInputFilePath, mOutputFolderPath)
            End If

            If success Then
                Return 0
            End If

            Return -1

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error occurred in modMain->Main", ex)
            Return -1
        End Try

    End Function

    Private Function GetAppVersion() As String
        Return Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() & " (" & PROGRAM_DATE & ")"
    End Function

    Private Function SetOptionsUsingCommandLineParameters(commandLineParser As clsParseCommandLine) As Boolean
        ' Returns True if no problems; otherwise, returns false

        Dim value As String = String.Empty
        Dim validParameters = New List(Of String) From {"I", "Org", "O", "Map", "Organism", "Prot", "Desc", "Verbose"}

        Try
            ' Make sure no invalid parameters are present
            If commandLineParser.InvalidParametersPresent(validParameters) Then
                ConsoleMsgUtils.ShowErrors("Invalid command line parameters", commandLineParser.InvalidParameters(validParameters))
                Return False
            Else

                ' Query objParseCommandLine to see if various parameters are present
                With commandLineParser
                    If .RetrieveValueForParameter("I", value) Then
                        mInputFilePath = value
                    ElseIf .NonSwitchParameterCount > 0 Then
                        mInputFilePath = .RetrieveNonSwitchParameter(0)
                    End If

                    If .RetrieveValueForParameter("Org", value) Then
                        mOrganismListFile = value
                    End If

                    If .RetrieveValueForParameter("Organism", value) Then
                        mOrganismName = value
                    End If

                    If .RetrieveValueForParameter("O", value) Then
                        mOutputFolderPath = value
                    End If

                    If .RetrieveValueForParameter("Prot", value) Then
                        mProteinListFile = value
                    End If

                    If .IsParameterPresent("Map") Then mCreateProteinToOrganismMapFile = True

                    If .IsParameterPresent("Desc") Then mSearchProteinDescriptions = True

                    If .IsParameterPresent("Verbose") Then mVerboseMode = True

                End With

                Return True
            End If

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error parsing the command line parameters", ex)
        End Try

        Return False

    End Function

    Private Sub ShowProgramHelp()

        Try
            Dim exeName = IO.Path.GetFileName(Reflection.Assembly.GetExecutingAssembly().Location)

            ConsoleMsgUtils.WrapParagraph(
                "This program reads a FASTA file and filters the proteins " +
                "by either organism name or protein name. For organism filtering, " +
                "the organism is determined using the protein description lines. " +
                "It optionally creates a filtered fasta file containing only the proteins of interest.")
            Console.WriteLine()

            Console.WriteLine("Program mode #1:" & ControlChars.NewLine & exeName & " SourceFile.fasta [/O:OutputFolderPath] [/Map] [/Verbose]")
            Console.WriteLine()

            Console.WriteLine("Program mode #2:" & ControlChars.NewLine & exeName & " SourceFile.fasta /Org:OrganismListFile.txt [/O:OutputFolderPath] [/Verbose]")
            Console.WriteLine()

            Console.WriteLine("Program mode #3:" & ControlChars.NewLine & exeName & " SourceFile.fasta /Organism:OrganismName [/O:OutputFolderPath] [/Verbose]")
            Console.WriteLine()

            Console.WriteLine("Program mode #4:" & ControlChars.NewLine & exeName & " SourceFile.fasta /Prot:ProteinListFile.txt [/O:OutputFolderPath] [/Desc] [/Verbose]")
            Console.WriteLine()

            ConsoleMsgUtils.WrapParagraph("The input file name is required. Surround the filename with double quotes if it contains spaces")
            Console.WriteLine()
            ConsoleMsgUtils.WrapParagraph(
                "Mode 1: will find the organisms present in the fasta file, " +
                "creating an OrganismSummary file. First looks for a UniProt sequence tag, " +
                "for example OS=Homo Sapiens.  If that tag is not found, then looks for the" +
                "name in the last set of square brackets in the protein description. " +
                "If OS= is missing and the square brackets are missing, searches the" +
                "entire description.")
            Console.WriteLine()
            ConsoleMsgUtils.WrapParagraph(
                "Use /Map to also create a file mapping protein name to organism name (filename SourceFasta_ProteinOrganismMap.txt")
            Console.WriteLine()
            ConsoleMsgUtils.WrapParagraph(
                "Mode 2: use /Org to specify a text file listing organism names " +
                "that should be used for filtering the fasta file. The program will create a " +
                "new fasta file that only contains proteins from the organisms of interest")
            Console.WriteLine()
            ConsoleMsgUtils.WrapParagraph(
                "The OrganismListFile should have one organism name per line" +
                "Entries that start with 'RegEx:' will be treated as regular expressions." +
                "Names or RegEx values are first matched to UniProt style OS=Organism entries" +
                "If not a match, the protein is skipped. If no OS= entry is present, next looks" +
                "for an organism name in square brackets. If no match to a [Organism] tag," +
                "the entire protein description is searched.")
            Console.WriteLine()
            ConsoleMsgUtils.WrapParagraph(
                "Mode 3: use /Organism to specify a single organism name" +
                "to be used for filtering the fasta file. The * character is treated as a wildcard. " +
                "The program will create a new fasta file that only contains proteins from that organism.")
            Console.WriteLine()
            ConsoleMsgUtils.WrapParagraph(
                "Mode 4: use /Prot to filter by protein name, using the proteins listed in the given text file.
                The program will create a new fasta file that only contains the listed proteins.")
            Console.WriteLine()
            ConsoleMsgUtils.WrapParagraph(
                "The ProteinListFile should have one protein name per line. " +
                "Protein names that start with 'RegEx:' will be treated as regular expressions for matching to protein names.")
            Console.WriteLine()
            ConsoleMsgUtils.WrapParagraph(
                "When using Mode 4, optionally use switch /Desc to indicate that protein descriptions should also be searched")
            Console.WriteLine()
            ConsoleMsgUtils.WrapParagraph(
                "For all 4 modes, use /O to specify an output folder. " +
                "If /O is missing, the output files will be created in the same folder as the source file")
            Console.WriteLine()
            ConsoleMsgUtils.WrapParagraph(
                "Use /Verbose to see details on each match, including the RegEx expression or search keyword that matches a protein name or description")
            Console.WriteLine()
            ConsoleMsgUtils.WrapParagraph("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2010")
            Console.WriteLine("Version: " & GetAppVersion())
            Console.WriteLine()

            Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or proteomics@pnnl.gov")
            Console.WriteLine("Website: https://omics.pnl.gov/ or https://github.com/pnnl-comp-mass-spec")
            Console.WriteLine()

            ' Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
            Threading.Thread.Sleep(750)

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error displaying the program syntax", ex)
        End Try

    End Sub

End Module
