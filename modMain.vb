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

    Public Const PROGRAM_DATE As String = "June 9, 2016"

	Private mInputFilePath As String
	Private mOutputFolderPath As String
    Private mOrganismListFile As String
    Private mOrganismName As String
    Private mProteinListFile As String

	Private mCreateProteinToOrganismMapFile As Boolean
    Private mSearchProteinDescriptions As Boolean
    Private mVerboseMode As Boolean

	Public Function Main() As Integer

        Dim returnCode As Integer

        Try
            ' Set the default values
            mInputFilePath = String.Empty

            mOrganismListFile = String.Empty
            mOrganismName = String.Empty
            mProteinListFile = String.Empty

            Dim proceed = False
            Dim oParseCommandLine As New clsParseCommandLine
            If oParseCommandLine.ParseCommandLine Then
                If SetOptionsUsingCommandLineParameters(oParseCommandLine) Then proceed = True
            End If

            If Not proceed OrElse
               oParseCommandLine.NeedToShowHelp OrElse
               oParseCommandLine.ParameterCount + oParseCommandLine.NonSwitchParameterCount = 0 OrElse
               mInputFilePath.Length = 0 Then
                ShowProgramHelp()
                returnCode = -1
            Else
                Dim oOrganismFilter = New clsFilterFastaByOrganism()

                With oOrganismFilter

                    .CreateProteinToOrganismMapFile = mCreateProteinToOrganismMapFile
                    .SearchProteinDescriptions = mSearchProteinDescriptions
                    .VerboseMode = mVerboseMode

                    ''If Not mParameterFilePath Is Nothing AndAlso mParameterFilePath.Length > 0 Then
                    ''    .LoadParameterFileSettings(mParameterFilePath)
                    ''End If
                End With

                Dim success = False

                If Not String.IsNullOrEmpty(mOrganismName) Then
                    success = oOrganismFilter.FilterFastaOneOrganism(mInputFilePath, mOrganismName, mOutputFolderPath)

                ElseIf Not String.IsNullOrEmpty(mOrganismListFile) Then
                    success = oOrganismFilter.FilterFastaByOrganism(mInputFilePath, mOrganismListFile, mOutputFolderPath)

                ElseIf Not String.IsNullOrEmpty(mProteinListFile) Then
                    success = oOrganismFilter.FilterFastaByProteinName(mInputFilePath, mProteinListFile, mOutputFolderPath)

                Else
                    success = oOrganismFilter.FindOrganismsInFasta(mInputFilePath, mOutputFolderPath)
                End If

                If success Then
                    returnCode = 0
                Else
                    returnCode = -1
                End If

            End If

        Catch ex As Exception
            returnCode = -1
            ConsoleMsgUtils.ShowError("Error occurred in modMain->Main", ex)
        End Try

		Return returnCode

	End Function

	Private Function GetAppVersion() As String
        Return Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() & " (" & PROGRAM_DATE & ")"
	End Function

    Private Function SetOptionsUsingCommandLineParameters(objParseCommandLine As clsParseCommandLine) As Boolean
        ' Returns True if no problems; otherwise, returns false

        Dim strValue As String = String.Empty
        Dim lstValidParameters = New List(Of String) From {"I", "Org", "O", "Map", "Organism", "Prot", "Desc", "Verbose"}

        Try
            ' Make sure no invalid parameters are present
            If commandLineParser.InvalidParametersPresent(validParameters) Then
                ConsoleMsgUtils.ShowErrors("Invalid command line parameters", commandLineParser.InvalidParameters(validParameters))
                Return False
            Else

                ' Query objParseCommandLine to see if various parameters are present
                With objParseCommandLine
                    If .RetrieveValueForParameter("I", strValue) Then
                        mInputFilePath = strValue
                    ElseIf .NonSwitchParameterCount > 0 Then
                        mInputFilePath = .RetrieveNonSwitchParameter(0)
                    End If

                    If .RetrieveValueForParameter("Org", strValue) Then
                        mOrganismListFile = strValue
                    End If

                    If .RetrieveValueForParameter("Organism", strValue) Then
                        mOrganismName = strValue
                    End If

                    If .RetrieveValueForParameter("O", strValue) Then
                        mOutputFolderPath = strValue
                    End If

                    If .RetrieveValueForParameter("Prot", strValue) Then
                        mProteinListFile = strValue
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

            Console.WriteLine("This program reads a FASTA file and filters the proteins " +
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

            Console.WriteLine("The input file name is required")
            Console.WriteLine("Surround the filename with double quotes if it contains spaces")
            Console.WriteLine()
            Console.WriteLine("Mode 1: will find the organisms present in the fasta file, ")
            Console.WriteLine("creating an OrganismSummary file. First looks for a Uniprot sequence tag,")
            Console.WriteLine("for example OS=Homo Sapiens.  If that tag is not found, then looks for the")
            Console.WriteLine("name in the last set of square brackets in the protein description.")
            Console.WriteLine("If OS= is missing and the square brackets are missing, searches the")
            Console.WriteLine("entire description.")
            Console.WriteLine()
            Console.WriteLine("Use /Map to also create a file mapping protein name to organism name")
            Console.WriteLine("(filename SourceFasta_ProteinOrganismMap.txt")
            Console.WriteLine()
            Console.WriteLine("Mode 2: use /Org to specify a text file listing organism names ")
            Console.WriteLine("that should be used for filtering the fasta file. The program will create a ")
            Console.WriteLine("new fasta file that only contains proteins from the organisms of interest")
            Console.WriteLine()
            Console.WriteLine("The OrganismListFile should have one organism name per line")
            Console.WriteLine("Entries that start with 'RegEx:' will be treated as regular expressions.")
            Console.WriteLine("Names or RegEx values are first matched to Uniprot style OS=Organism entries")
            Console.WriteLine("If not a match, the protein is skipped. If no OS= entry is present, next looks")
            Console.WriteLine("for an organism name in square brackets. If no match to a [Organism] tag,")
            Console.WriteLine("the entire protein description is searched.")
            Console.WriteLine()
            Console.WriteLine("Mode 3: use /Organism to specify a single organism name")
            Console.WriteLine("to be used for filtering the fasta file. The * character is treated as a wildcard. ")
            Console.WriteLine("The program will create a new fasta file that only contains proteins from that ")
            Console.WriteLine("organism.")
            Console.WriteLine()
            Console.WriteLine("Mode 4: use /Prot to filter by protein name, using the proteins listed in the given text file. ")
            Console.WriteLine("The program will create a new fasta file that only contains the listed proteins.")
            Console.WriteLine()
            Console.WriteLine("The ProteinListFile should have one protein name per line")
            Console.WriteLine("Protein names that start with 'RegEx:' will be treated as regular expressions ")
            Console.WriteLine("for matching to protein names.")
            Console.WriteLine()
            Console.WriteLine("When using Mode 4, optionally use switch /Desc to indicate that protein descriptions should also be searched")
            Console.WriteLine()
            Console.WriteLine("For all 4 modes, use /O to specify an output folder")
            Console.WriteLine("If /O is missing, the output files will be created in the same folder as the source file")
            Console.WriteLine()
            Console.WriteLine("Use /Verbose to see details on each match, including the RegEx expression or search keyword that matches a protein name or description")
            Console.WriteLine()
            Console.WriteLine("Program written by Matthew Monroe for the Department of Energy (PNNL, Richland, WA) in 2010")
            Console.WriteLine("Version: " & GetAppVersion())
            Console.WriteLine()

            Console.WriteLine("E-mail: matthew.monroe@pnnl.gov or matt@alchemistmatt.com")
            Console.WriteLine("Website: http://omics.pnl.gov/ or http://panomics.pnnl.gov/")
            Console.WriteLine()

			' Delay for 750 msec in case the user double clicked this file from within Windows Explorer (or started the program via a shortcut)
            Threading.Thread.Sleep(750)

        Catch ex As Exception
            ConsoleMsgUtils.ShowError("Error displaying the program syntax", ex)
        End Try

    End Sub

End Module
